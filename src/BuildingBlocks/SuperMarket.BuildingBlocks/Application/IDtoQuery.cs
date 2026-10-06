namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// Query contract for retrieving a single item (e.g. GetById).
/// The response is constrained to IResponseDto; pagination is intentionally not required.
/// </summary>
public interface IDtoQuery<TResponse> : IQuery<TResponse>
    where TResponse : IResponseDto
{
}

public interface IDtoQueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : IDtoQuery<TResponse>
    where TResponse : IResponseDto
{
}
