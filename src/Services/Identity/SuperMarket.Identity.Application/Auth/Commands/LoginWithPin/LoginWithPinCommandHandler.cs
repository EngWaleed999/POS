// -------------------------------------------------------------------------
// Business Logic Flow:
// 1. [Pre-flight Validation]: Find cashier user by username and verify active non-deleted status.
// 2. [Security Lockout Check]: Enforce account lockout cooldown period on repeated failures.
// 3. [PIN Verification]: Constant-time cryptographic verification of PBKDF2 PIN hash.
// 4. [Terminal Validation]: Verify POS register terminal status and store branch affinity.
// 5. [State Mutation & Audit]: Record successful login timestamp and persist to database.
// 6. [Authorization Resolution]: Fetch role and retrieve assigned granular permissions.
// 7. [Token Issuance]: Delegate token generation to Keycloak and extract accurate expires_in.
// -------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Cryptography;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Auth.Commands.LoginWithPin;

public sealed class LoginWithPinCommandHandler : ICommandHandler<LoginWithPinCommand, CashierLoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPOSRegisterRepository _posRegisterRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPinHasher _pinHasher;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IIdentityReadDbContext _readDb;
    private readonly IUnitOfWork _unitOfWork;

    public LoginWithPinCommandHandler(
        IUserRepository userRepository,
        IPOSRegisterRepository posRegisterRepository,
        IRoleRepository roleRepository,
        IPinHasher pinHasher,
        IIdentityProviderService identityProviderService,
        IIdentityReadDbContext readDb,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _posRegisterRepository = posRegisterRepository;
        _roleRepository = roleRepository;
        _pinHasher = pinHasher;
        _identityProviderService = identityProviderService;
        _readDb = readDb;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashierLoginResponse>> Handle(LoginWithPinCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // --- 1. Pre-flight Validation: Find cashier user and verify active status ---
        var user = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.InvalidCredentials);
        }

        // --- 2. Security Lockout Check: Enforce account lockout cooldown ---
        if (user.IsLockedOut(now))
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.AccountLockedOut);
        }

        // --- 3. PIN Verification: Constant-time cryptographic verification of PBKDF2 PIN hash ---
        if (string.IsNullOrWhiteSpace(user.PinHash))
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.PinNotConfigured);
        }

        bool isPinValid = _pinHasher.VerifyPin(command.Pin, user.PinHash);
        if (!isPinValid)
        {
            user.RecordFailedLogin(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<CashierLoginResponse>(UserErrors.InvalidCredentials);
        }

        // --- 4. Terminal Validation: Verify POS register terminal status and store branch affinity ---
        var register = await _posRegisterRepository.GetByIdAsync(command.RegisterId, cancellationToken);
        if (register is null || !register.IsActive || register.IsDeleted)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.RegisterNotFound);
        }

        if (user.BranchId.HasValue && user.BranchId.Value != register.BranchId)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.BranchMismatch);
        }

        // --- 5. State Mutation & Audit: Record successful login timestamp and persist to database ---
        user.RecordLogin(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- 6. Authorization Resolution: Fetch role and retrieve assigned granular permissions ---
        var role = await _roleRepository.GetByIdAsync(user.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.RoleNotFound);
        }

        var permissions = await (
            from rpg in _readDb.RolePermissionGroups
            where rpg.RoleId == user.RoleId
            join pgi in _readDb.PermissionGroupItems on rpg.GroupId equals pgi.GroupId
            join p in _readDb.Permissions on pgi.PermissionId equals p.Id
            select $"{p.Resource}:{p.Action}:{p.Scope}".ToLowerInvariant()
        ).Distinct().ToListAsync(cancellationToken);

        // --- 7. Token Issuance: Delegate token generation to Keycloak and extract accurate expires_in ---
        var tokenResult = await _identityProviderService.GenerateUserTokenAsync(user.KeycloakUserId, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<CashierLoginResponse>(tokenResult.Error);
        }

        return Result.Success(new CashierLoginResponse(
            AccessToken: tokenResult.Value.AccessToken,
            ExpiresInSeconds: tokenResult.Value.ExpiresIn,
            CashierId: user.Id,
            Username: user.Username,
            FullName: user.FullName,
            RoleId: role.Id,
            RoleName: role.RoleName,
            BranchId: register.BranchId,
            RegisterId: register.Id,
            Permissions: permissions));
    }
}
