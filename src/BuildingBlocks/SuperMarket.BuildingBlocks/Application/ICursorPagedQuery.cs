namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// Query contract for keyset/cursor-paginated collections (high-volume feeds such as sales and transactions).
/// Enforces: DTO items, mandatory CursorParams input, and CursorPagedList output (no COUNT(*) scan).
/// </summary>
public interface ICursorPagedQuery<TResponse, TCursor> : IQuery<CursorPagedList<TResponse, TCursor>>
    where TResponse : IResponseDto
{
    CursorParams<TCursor> CursorPagination { get; }
}

public interface ICursorPagedQueryHandler<TQuery, TResponse, TCursor>
    : IQueryHandler<TQuery, CursorPagedList<TResponse, TCursor>>
    where TQuery : ICursorPagedQuery<TResponse, TCursor>
    where TResponse : IResponseDto
{
}
