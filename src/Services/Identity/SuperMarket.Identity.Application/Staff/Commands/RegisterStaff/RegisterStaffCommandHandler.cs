// Orchestrates staff registration across the external Identity Provider and local domain persistence.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.RegisterStaff;

public sealed class RegisterStaffCommandHandler : ICommandHandler<RegisterStaffCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterStaffCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IBranchRepository branchRepository,
        IIdentityProviderService identityProviderService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _branchRepository = branchRepository;
        _identityProviderService = identityProviderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RegisterStaffCommand command, CancellationToken cancellationToken)
    {
        // --- 1. Pre-flight Validation: Verify Role Existence ---
        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure<Guid>(UserErrors.RoleNotFound);
        }

        // --- 2. Pre-flight Validation: Verify Branch Existence (if assigned) ---
        if (command.BranchId.HasValue)
        {
            var branch = await _branchRepository.GetByIdAsync(command.BranchId.Value, cancellationToken);
            if (branch is null || !branch.IsActive)
            {
                return Result.Failure<Guid>(UserErrors.BranchNotFound);
            }
        }

        // --- 3. Pre-flight Validation: Enforce Local Uniqueness ---
        if (await _userRepository.ExistsByUsernameAsync(command.Username, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.UsernameAlreadyExists);
        }

        if (await _userRepository.ExistsByPhoneNumberAsync(command.PhoneNumber, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.PhoneNumberAlreadyExists);
        }

        if (!string.IsNullOrWhiteSpace(command.Email) && await _userRepository.ExistsByEmailAsync(command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists);
        }

        // --- 4. Distributed Step 1: Provision Account in External Identity Provider (Keycloak) ---
        var idpResult = await _identityProviderService.CreateUserAsync(
            command.Username,
            command.Email,
            command.PhoneNumber,
            command.FullName,
            command.InitialPassword,
            cancellationToken);

        if (idpResult.IsFailure)
        {
            return Result.Failure<Guid>(idpResult.Error);
        }

        var keycloakUserId = idpResult.Value;

        // Assign initial role in Keycloak
        var roleAssignResult = await _identityProviderService.AssignRoleAsync(keycloakUserId, role.RoleName, cancellationToken);
        if (roleAssignResult.IsFailure)
        {
            // Compensating Rollback: Remove orphaned account from Identity Provider
            await _identityProviderService.DeleteUserAsync(keycloakUserId, CancellationToken.None);
            return Result.Failure<Guid>(roleAssignResult.Error);
        }

        // --- 5. Distributed Step 2: Construct Domain Aggregate Root ---
        var userResult = User.Create(
            username: command.Username,
            phoneNumber: command.PhoneNumber,
            keycloakUserId: keycloakUserId,
            fullName: command.FullName,
            roleId: command.RoleId,
            email: command.Email,
            branchId: command.BranchId);

        if (userResult.IsFailure)
        {
            // Compensating Rollback
            await _identityProviderService.DeleteUserAsync(keycloakUserId, CancellationToken.None);
            return Result.Failure<Guid>(userResult.Error);
        }

        var user = userResult.Value;
        _userRepository.Add(user);

        // --- 6. Distributed Step 3: Persist to Local Database with Failure Compensation ---
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Compensating Rollback: Guarantee no orphaned identity remains in Keycloak on DB commit failure
            await _identityProviderService.DeleteUserAsync(keycloakUserId, CancellationToken.None);
            throw;
        }

        return Result.Success(user.Id);
    }
}
