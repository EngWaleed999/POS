namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// Query contract for offset-paginated collections (administrative tables needing page jumps and total count).
/// Enforces: DTO items, mandatory PaginationParams input, and PagedList output.
/// Handlers should build the result via PagedList&lt;T&gt;.CreateAsync on a projected IQueryable.
/// </summary>
public interface IPagedQuery<TResponse> : IQuery<PagedList<TResponse>>
    where TResponse : IResponseDto
{
    PaginationParams Pagination { get; }
}

public interface IPagedQueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, PagedList<TResponse>>
    where TQuery : IPagedQuery<TResponse>
    where TResponse : IResponseDto
{
}
