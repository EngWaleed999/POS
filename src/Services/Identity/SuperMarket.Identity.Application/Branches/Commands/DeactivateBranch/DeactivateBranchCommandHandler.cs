using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Branches.Commands.DeactivateBranch;

/// <summary>
/// Orchestrates the deactivation of a retail branch.
/// Applies domain invariants preventing operations on already-deactivated or deleted branches.
/// </summary>
public sealed class DeactivateBranchCommandHandler(
    IBranchRepository _branchRepository,
    IUnitOfWork _unitOfWork) : ICommandHandler<DeactivateBranchCommand>
{
    private readonly IBranchRepository _branchRepository = _branchRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result> Handle(
        DeactivateBranchCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
        {
            return Result.Failure(BranchErrors.NotFound);
        }

        var deactivateResult = branch.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return deactivateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
