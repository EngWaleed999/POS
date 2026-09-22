// User Aggregate Root: Represents staff members, encapsulating identity, Keycloak mapping, Cashier PIN, and domain lifecycle events.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class User : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable, IActivatable
{
    // -------------------------------------------------------------------------
    // Core Identity Properties
    // -------------------------------------------------------------------------
    public string Username { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;
    public string? Email { get; private set; }
    public string KeycloakUserId { get; private set; } = default!;
    public string? PinHash { get; private set; }
    public string FullName { get; private set; } = default!;
    public Guid RoleId { get; private set; }
    public Guid? BranchId { get; private set; }
    public DateTimeOffset? LastLogin { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }

    // -------------------------------------------------------------------------
    // IActivatable (Protected via Explicit Interface Implementation)
    // -------------------------------------------------------------------------
    public bool IsActive { get; private set; } = true;

    bool IActivatable.IsActive
    {
        get => IsActive;
        set => IsActive = value;
    }

    // -------------------------------------------------------------------------
    // ISoftDeletable (Protected via Explicit Interface Implementation)
    // -------------------------------------------------------------------------
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }
    public string? DeletionReason { get; private set; }

    bool ISoftDeletable.IsDeleted
    {
        get => IsDeleted;
        set => IsDeleted = value;
    }

    DateTimeOffset? ISoftDeletable.DeletedAt
    {
        get => DeletedAt;
        set => DeletedAt = value;
    }

    string? ISoftDeletable.DeletedBy
    {
        get => DeletedBy;
        set => DeletedBy = value;
    }

    // -------------------------------------------------------------------------
    // IAuditableEntity (Protected via Explicit Interface Implementation)
    // -------------------------------------------------------------------------
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    DateTimeOffset IAuditableEntity.CreatedAt
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    string? IAuditableEntity.CreatedBy
    {
        get => CreatedBy;
        set => CreatedBy = value;
    }

    DateTimeOffset? IAuditableEntity.UpdatedAt
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    string? IAuditableEntity.UpdatedBy
    {
        get => UpdatedBy;
        set => UpdatedBy = value;
    }

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private User()
    {
    }

    private User(
        Guid id,
        string username,
        string phoneNumber,
        string? email,
        string keycloakUserId,
        string fullName,
        Guid roleId,
        Guid? branchId)
        : base(id)
    {
        Username = username;
        PhoneNumber = phoneNumber;
        Email = email;
        KeycloakUserId = keycloakUserId;
        FullName = fullName;
        RoleId = roleId;
        BranchId = branchId;
        IsActive = true;
        IsDeleted = false;
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<User> Create(
        string username,
        string phoneNumber,
        string keycloakUserId,
        string fullName,
        Guid roleId,
        string? email = null,
        Guid? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Result.Failure<User>(UserErrors.EmptyUsername);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result.Failure<User>(UserErrors.EmptyPhoneNumber);

        if (string.IsNullOrWhiteSpace(keycloakUserId))
            return Result.Failure<User>(UserErrors.EmptyKeycloakUserId);

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<User>(UserErrors.EmptyFullName);

        if (roleId == Guid.Empty)
            return Result.Failure<User>(UserErrors.EmptyRoleId);

        string? sanitizedEmail = string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToLowerInvariant();

        var user = new User(
            id: Guid.NewGuid(),
            username: username.Trim(),
            phoneNumber: phoneNumber.Trim(),
            email: sanitizedEmail,
            keycloakUserId: keycloakUserId.Trim(),
            fullName: fullName.Trim(),
            roleId: roleId,
            branchId: branchId);

        user.AddDomainEvent(new UserCreatedDomainEvent(
            user.Id,
            user.Username,
            user.PhoneNumber,
            user.Email,
            user.FullName,
            user.RoleId,
            user.BranchId));

        return Result.Success(user);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result SetPin(string pinHash)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (string.IsNullOrWhiteSpace(pinHash))
            return Result.Failure(UserErrors.EmptyPinHash);

        PinHash = pinHash;
        return Result.Success();
    }

    public Result RemovePin()
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        PinHash = null;
        return Result.Success();
    }

    public Result UpdateProfile(string fullName)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure(UserErrors.EmptyFullName);

        FullName = fullName.Trim();
        return Result.Success();
    }

    public Result UpdateContactInfo(string phoneNumber, string? email)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result.Failure(UserErrors.EmptyPhoneNumber);

        string? sanitizedEmail = string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToLowerInvariant();

        PhoneNumber = phoneNumber.Trim();
        Email = sanitizedEmail;
        return Result.Success();
    }

    public Result ChangeRole(Guid newRoleId)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (newRoleId == Guid.Empty)
            return Result.Failure(UserErrors.EmptyRoleId);

        if (RoleId == newRoleId)
            return Result.Success();

        var oldRoleId = RoleId;
        RoleId = newRoleId;

        AddDomainEvent(new UserRoleChangedDomainEvent(Id, oldRoleId, newRoleId));

        return Result.Success();
    }

    public Result AssignToBranch(Guid? newBranchId)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (BranchId == newBranchId)
            return Result.Success();

        var oldBranchId = BranchId;
        BranchId = newBranchId;

        AddDomainEvent(new UserTransferredDomainEvent(Id, oldBranchId, newBranchId));

        return Result.Success();
    }

    public Result Activate()
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (IsActive)
            return Result.Failure(UserErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (!IsActive)
            return Result.Failure(UserErrors.AlreadyDeactivated);

        IsActive = false;

        AddDomainEvent(new UserDeactivatedDomainEvent(Id));

        return Result.Success();
    }

    public Result SoftDelete(string deletedBy, string? reason = null)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.AlreadyDeleted);

        IsDeleted = true;
        DeletedBy = string.IsNullOrWhiteSpace(deletedBy) ? "SYSTEM" : deletedBy.Trim();
        DeletionReason = reason?.Trim();
        IsActive = false;

        AddDomainEvent(new UserDeactivatedDomainEvent(Id));

        return Result.Success();
    }

    public bool IsLockedOut(DateTimeOffset currentTime)
    {
        return LockoutEnd.HasValue && LockoutEnd.Value > currentTime;
    }

    public Result RecordFailedLogin(DateTimeOffset currentTime, int maxFailedAttempts = 3, TimeSpan? lockoutDuration = null)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (IsLockedOut(currentTime))
            return Result.Failure(UserErrors.AccountLockedOut);

        AccessFailedCount++;

        if (AccessFailedCount >= maxFailedAttempts)
        {
            var duration = lockoutDuration ?? TimeSpan.FromMinutes(15);
            LockoutEnd = currentTime.Add(duration);

            AddDomainEvent(new UserLockedOutDomainEvent(Id, Username, AccessFailedCount, LockoutEnd.Value));
        }

        return Result.Success();
    }

    public Result Unlock()
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        AccessFailedCount = 0;
        LockoutEnd = null;

        return Result.Success();
    }

    public Result RecordLogin(DateTimeOffset loginTime)
    {
        if (IsDeleted)
            return Result.Failure(UserErrors.DeletedUserCannotBeModified);

        if (IsLockedOut(loginTime))
            return Result.Failure(UserErrors.AccountLockedOut);

        AccessFailedCount = 0;
        LockoutEnd = null;
        LastLogin = loginTime;

        return Result.Success();
    }
}
