using System.Collections.Generic;

namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// Query contract for retrieving small, unpaginated collections of DTOs (e.g. lookups, dropdowns, static catalogs).
/// Constrains the individual item type to IResponseDto and wraps the output in an immutable IReadOnlyList.
/// </summary>
public interface IListQuery<TResponse> : IQuery<IReadOnlyList<TResponse>>
    where TResponse : IResponseDto
{
}

/// <summary>
/// Query handler contract corresponding to IListQuery.
/// Enforces returning Result<IReadOnlyList<TResponse>> where each element is an IResponseDto.
/// </summary>
public interface IListQueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, IReadOnlyList<TResponse>>
    where TQuery : IListQuery<TResponse>
    where TResponse : IResponseDto
{
}
