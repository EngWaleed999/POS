// Branch Aggregate Root: Represents a physical supermarket retail store branch.
// Encapsulates physical address (ValueObject), tax credentials, operational activation, and operating hours.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class Branch : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable, IActivatable
{
    private readonly List<BranchOperatingHours> _operatingHours = [];

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public BranchCode Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Address Address { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; private set; }
    public string TaxNumber { get; private set; } = default!;
    public string Currency { get; private set; } = "YE";

    public IReadOnlyCollection<BranchOperatingHours> OperatingHours => _operatingHours.AsReadOnly();

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
    private Branch()
    {
    }

    private Branch(
        Guid id,
        BranchCode code,
        string name,
        Address address,
        string phone,
        string taxNumber,
        string? email,
        string currency)
        : base(id)
    {
        Code = code;
        Name = name;
        Address = address;
        Phone = phone;
        TaxNumber = taxNumber;
        Email = email;
        Currency = currency;
        IsActive = true;
        IsDeleted = false;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<Branch> Create(
        BranchCode code,
        string name,
        Address address,
        string phone,
        string taxNumber,
        string? email = null,
        string currency = "SAR")
    {
        if (code is null)
            return Result.Failure<Branch>(BranchErrors.EmptyBranchCode);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Branch>(BranchErrors.EmptyBranchName);

        if (address is null)
            return Result.Failure<Branch>(BranchErrors.EmptyStreet);

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<Branch>(BranchErrors.EmptyPhone);

        if (string.IsNullOrWhiteSpace(taxNumber))
            return Result.Failure<Branch>(BranchErrors.EmptyTaxNumber);

        if (string.IsNullOrWhiteSpace(currency))
            return Result.Failure<Branch>(BranchErrors.EmptyCurrency);

        var branch = new Branch(
            id: Guid.NewGuid(),
            code: code,
            name: name.Trim(),
            address: address,
            phone: phone.Trim(),
            taxNumber: taxNumber.Trim(),
            email: string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            currency: currency.Trim().ToUpperInvariant());

        branch.AddDomainEvent(new BranchCreatedDomainEvent(
            branch.Id,
            branch.Code.Value,
            branch.Name,
            branch.Address.City,
            branch.Phone,
            branch.TaxNumber));

        return Result.Success(branch);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result UpdateDetails(string name, string phone, string taxNumber, string? email = null)
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(BranchErrors.EmptyBranchName);

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure(BranchErrors.EmptyPhone);

        if (string.IsNullOrWhiteSpace(taxNumber))
            return Result.Failure(BranchErrors.EmptyTaxNumber);

        Name = name.Trim();
        Phone = phone.Trim();
        TaxNumber = taxNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        return Result.Success();
    }

    public Result UpdateAddress(Address newAddress)
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        if (newAddress is null)
            return Result.Failure(BranchErrors.EmptyStreet);

        Address = newAddress;
        return Result.Success();
    }

    public Result SetOperatingHours(IEnumerable<BranchOperatingHours> hours)
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        if (hours is null)
            return Result.Failure(BranchErrors.OperatingHoursNull);

        var hoursList = hours.ToList();

        // Enforce uniqueness of DayOfWeek
        var distinctCount = hoursList.Select(h => h.DayOfWeek).Distinct().Count();
        if (distinctCount != hoursList.Count)
            return Result.Failure(BranchErrors.DuplicateDayOfWeek);

        _operatingHours.Clear();
        _operatingHours.AddRange(hoursList);

        return Result.Success();
    }

    public Result AddOrUpdateOperatingHour(
        DayOfWeek dayOfWeek,
        TimeOnly openTime,
        TimeOnly closeTime,
        bool isClosed = false)
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        var existing = _operatingHours.FirstOrDefault(h => h.DayOfWeek == dayOfWeek);
        if (existing is not null)
        {
            return existing.Update(openTime, closeTime, isClosed);
        }

        var createResult = BranchOperatingHours.Create(Id, dayOfWeek, openTime, closeTime, isClosed);
        if (createResult.IsFailure)
            return Result.Failure(createResult.Error);

        _operatingHours.Add(createResult.Value);
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        if (IsActive)
            return Result.Failure(BranchErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.DeletedBranchCannotBeModified);

        if (!IsActive)
            return Result.Failure(BranchErrors.AlreadyDeactivated);

        IsActive = false;
        return Result.Success();
    }

    public Result SoftDelete(string? deletedBy = null)
    {
        if (IsDeleted)
            return Result.Failure(BranchErrors.AlreadyDeleted);

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
        IsActive = false;

        return Result.Success();
    }
}
