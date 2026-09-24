// BranchOperatingHours Entity: Represents scheduled operating hours for a single day of the week for a branch.
// Designed as a child entity governed exclusively within the Branch aggregate root.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class BranchOperatingHours : Entity<Guid>
{
    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public Guid BranchId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpenTime { get; private set; }
    public TimeOnly CloseTime { get; private set; }
    public bool IsClosed { get; private set; }

    // Overnight shift spans across midnight into next calendar day (e.g. 16:00 to 02:00)
    public bool IsOvernight => !IsClosed && CloseTime < OpenTime;

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private BranchOperatingHours()
    {
    }

    private BranchOperatingHours(
        Guid id,
        Guid branchId,
        DayOfWeek dayOfWeek,
        TimeOnly openTime,
        TimeOnly closeTime,
        bool isClosed)
        : base(id)
    {
        BranchId = branchId;
        DayOfWeek = dayOfWeek;
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<BranchOperatingHours> Create(
        Guid branchId,
        DayOfWeek dayOfWeek,
        TimeOnly openTime,
        TimeOnly closeTime,
        bool isClosed = false)
    {
        if (branchId == Guid.Empty)
            return Result.Failure<BranchOperatingHours>(BranchOperatingHoursErrors.EmptyBranchId);

        if (!isClosed && openTime == closeTime)
            return Result.Failure<BranchOperatingHours>(BranchOperatingHoursErrors.SameOpenAndCloseTime);

        var record = new BranchOperatingHours(
            id: Guid.NewGuid(),
            branchId: branchId,
            dayOfWeek: dayOfWeek,
            openTime: isClosed ? TimeOnly.MinValue : openTime,
            closeTime: isClosed ? TimeOnly.MinValue : closeTime,
            isClosed: isClosed);

        return Result.Success(record);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result Update(TimeOnly openTime, TimeOnly closeTime, bool isClosed)
    {
        if (!isClosed && openTime == closeTime)
            return Result.Failure(BranchOperatingHoursErrors.SameOpenAndCloseTime);

        OpenTime = isClosed ? TimeOnly.MinValue : openTime;
        CloseTime = isClosed ? TimeOnly.MinValue : closeTime;
        IsClosed = isClosed;

        return Result.Success();
    }
}
