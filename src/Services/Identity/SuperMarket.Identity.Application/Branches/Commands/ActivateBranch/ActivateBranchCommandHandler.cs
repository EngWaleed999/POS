using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Branches.Commands.ActivateBranch;

/// <summary>
/// Orchestrates the activation/reactivation of a retail branch.
/// Applies domain invariants preventing operations on already-active or deleted branches.
/// </summary>
public sealed class ActivateBranchCommandHandler(
    IBranchRepository _branchRepository,
    IUnitOfWork _unitOfWork) : ICommandHandler<ActivateBranchCommand>
{
    private readonly IBranchRepository _branchRepository = _branchRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result> Handle(
        ActivateBranchCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
        {
            return Result.Failure(BranchErrors.NotFound);
        }

        var activateResult = branch.Activate();
        if (activateResult.IsFailure)
        {
            return activateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
