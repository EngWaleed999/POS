// Orchestrates cashier fast-login verification, lockout enforcement, and session token generation.

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

        // --- 1. Find User by Username ---
        var user = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.InvalidCredentials);
        }

        // --- 2. Check Lockout State ---
        if (user.IsLockedOut(now))
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.AccountLockedOut);
        }

        // --- 3. Verify PIN Hash Configuration ---
        if (string.IsNullOrWhiteSpace(user.PinHash))
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.PinNotConfigured);
        }

        // --- 4. Verify Cryptographic PIN Hash (Constant-Time) ---
        bool isPinValid = _pinHasher.VerifyPin(command.Pin, user.PinHash);
        if (!isPinValid)
        {
            user.RecordFailedLogin(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<CashierLoginResponse>(UserErrors.InvalidCredentials);
        }

        // --- 5. Verify POS Register Terminal Existence & Active Status ---
        var register = await _posRegisterRepository.GetByIdAsync(command.RegisterId, cancellationToken);
        if (register is null || !register.IsActive || register.IsDeleted)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.RegisterNotFound);
        }

        // --- 6. Verify Store Branch Affinity ---
        if (user.BranchId.HasValue && user.BranchId.Value != register.BranchId)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.BranchMismatch);
        }

        // --- 7. Record Successful Login in Aggregate ---
        user.RecordLogin(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- 8. Fetch Role Details ---
        var role = await _roleRepository.GetByIdAsync(user.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure<CashierLoginResponse>(UserErrors.RoleNotFound);
        }

        // --- 9. Retrieve Granular Permissions Assigned to Role ---
        var permissions = await (
            from rpg in _readDb.RolePermissionGroups
            where rpg.RoleId == user.RoleId
            join pgi in _readDb.PermissionGroupItems on rpg.GroupId equals pgi.GroupId
            join p in _readDb.Permissions on pgi.PermissionId equals p.Id
            select $"{p.Action}:{p.Resource}:{p.Scope}"
        ).Distinct().ToListAsync(cancellationToken);

        // --- 10. Delegate Token Issuance to External Identity Provider (Keycloak) ---
        var tokenResult = await _identityProviderService.GenerateUserTokenAsync(user.KeycloakUserId, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<CashierLoginResponse>(tokenResult.Error);
        }

        const int tokenExpirySeconds = 28800; // 8 hours standard cashier shift duration

        return Result.Success(new CashierLoginResponse(
            AccessToken: tokenResult.Value,
            ExpiresInSeconds: tokenExpirySeconds,
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
