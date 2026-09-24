// POSRegister Aggregate Root: Represents a physical Point of Sale terminal device assigned to a specific store branch.
// Encapsulates hardware identification (TerminalIpOrFingerprint), branch affinity, operational status, and auditability.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class POSRegister : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable, IActivatable
{
    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public Guid BranchId { get; private set; }
    public RegisterCode RegisterCode { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? TerminalIpOrFingerprint { get; private set; }

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
    private POSRegister()
    {
    }

    private POSRegister(
        Guid id,
        Guid branchId,
        RegisterCode registerCode,
        string name,
        string? terminalIpOrFingerprint)
        : base(id)
    {
        BranchId = branchId;
        RegisterCode = registerCode;
        Name = name;
        TerminalIpOrFingerprint = terminalIpOrFingerprint;
        IsActive = true;
        IsDeleted = false;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<POSRegister> Create(
        Guid branchId,
        RegisterCode registerCode,
        string name,
        string? terminalIpOrFingerprint = null)
    {
        if (branchId == Guid.Empty)
            return Result.Failure<POSRegister>(POSRegisterErrors.EmptyBranchId);

        if (registerCode is null)
            return Result.Failure<POSRegister>(POSRegisterErrors.EmptyRegisterCode);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<POSRegister>(POSRegisterErrors.EmptyRegisterName);

        var register = new POSRegister(
            id: Guid.NewGuid(),
            branchId: branchId,
            registerCode: registerCode,
            name: name.Trim(),
            terminalIpOrFingerprint: string.IsNullOrWhiteSpace(terminalIpOrFingerprint)
                ? null
                : terminalIpOrFingerprint.Trim());

        register.AddDomainEvent(new POSRegisterCreatedDomainEvent(
            register.Id,
            register.BranchId,
            register.RegisterCode.Value,
            register.Name,
            register.TerminalIpOrFingerprint));

        return Result.Success(register);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result UpdateDetails(string name, string? terminalIpOrFingerprint)
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.DeletedRegisterCannotBeModified);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(POSRegisterErrors.EmptyRegisterName);

        Name = name.Trim();
        TerminalIpOrFingerprint = string.IsNullOrWhiteSpace(terminalIpOrFingerprint)
            ? null
            : terminalIpOrFingerprint.Trim();

        return Result.Success();
    }

    public Result UpdateFingerprint(string? terminalIpOrFingerprint)
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.DeletedRegisterCannotBeModified);

        TerminalIpOrFingerprint = string.IsNullOrWhiteSpace(terminalIpOrFingerprint)
            ? null
            : terminalIpOrFingerprint.Trim();

        return Result.Success();
    }

    public Result ReassignBranch(Guid newBranchId)
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.DeletedRegisterCannotBeModified);

        if (newBranchId == Guid.Empty)
            return Result.Failure(POSRegisterErrors.EmptyBranchId);

        if (BranchId == newBranchId)
            return Result.Failure(POSRegisterErrors.SameBranchReassignment);

        BranchId = newBranchId;
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.DeletedRegisterCannotBeModified);

        if (IsActive)
            return Result.Failure(POSRegisterErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.DeletedRegisterCannotBeModified);

        if (!IsActive)
            return Result.Failure(POSRegisterErrors.AlreadyDeactivated);

        IsActive = false;
        return Result.Success();
    }

    public Result SoftDelete(string? deletedBy = null)
    {
        if (IsDeleted)
            return Result.Failure(POSRegisterErrors.AlreadyDeleted);

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
        IsActive = false;

        return Result.Success();
    }
}
