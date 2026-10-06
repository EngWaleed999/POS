 using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Branches.Queries.GetBranchById;

/// <summary>
/// Query handler that retrieves branch details directly from the read-optimized DbContext.
/// Applies projection in SQL using named arguments, deterministic ordering, and SQL-translatable expressions.
/// </summary>
public sealed class GetBranchByIdQueryHandler : IDtoQueryHandler<GetBranchByIdQuery, BranchDetailResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public GetBranchByIdQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<BranchDetailResponse>> Handle(
        GetBranchByIdQuery query,
        CancellationToken cancellationToken)
    {
        var branch = await _readDb.Branches
            .Where(b => b.Id == query.BranchId)
            .Select(b => new BranchDetailResponse(
                Id: b.Id,
                Code: b.Code.Value,
                Name: b.Name,
                Street: b.Address.Street,
                City: b.Address.City,
                Region: b.Address.Region,
                PostalCode: b.Address.PostalCode,
                Phone: b.Phone,
                TaxNumber: b.TaxNumber,
                Email: b.Email,
                Currency: b.Currency,
                IsActive: b.IsActive,
                CreatedAt: b.CreatedAt,
                OperatingHours: b.OperatingHours
                    .OrderBy(h => h.DayOfWeek)
                    .Select(h => new BranchOperatingHoursResponse(
                        DayOfWeek: h.DayOfWeek,
                        OpenTime: h.OpenTime,
                        CloseTime: h.CloseTime,
                        IsClosed: h.IsClosed,
                        IsOvernight: !h.IsClosed && h.CloseTime < h.OpenTime))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (branch is null)
        {
            return Result.Failure<BranchDetailResponse>(BranchErrors.NotFound);
        }

        return Result.Success(branch);
    }
}
