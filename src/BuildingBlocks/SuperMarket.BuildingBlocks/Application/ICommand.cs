using MediatR;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.Application;


public interface ICommand : IRequest<Result>
{
}


public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}



public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}


public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
