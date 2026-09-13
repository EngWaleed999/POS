using MediatR;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.Application;


public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}


public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
