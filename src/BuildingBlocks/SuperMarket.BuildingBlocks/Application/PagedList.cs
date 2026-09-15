using Microsoft.EntityFrameworkCore;

namespace SuperMarket.BuildingBlocks.Application;

public sealed class PagedList<T>
{
    // -------------------------------------------------------------------------
    // Pagination Payload & Metadata
    // -------------------------------------------------------------------------
    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedList(IReadOnlyCollection<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    // -------------------------------------------------------------------------
    // Async Database Execution Factory (EF Core Optimized)
    // -------------------------------------------------------------------------
    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        int maxPageSize = PaginationParams.FallbackMaxPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var safePageNumber = Math.Max(1, pageNumber);
        var safePageSize = Math.Clamp(pageSize, 1, maxPageSize);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<T>(items, safePageNumber, safePageSize, totalCount);
    }
}
