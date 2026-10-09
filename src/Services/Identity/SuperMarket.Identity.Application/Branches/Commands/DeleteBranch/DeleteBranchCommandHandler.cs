using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Branches.Commands.DeleteBranch;

/// <summary>
/// Orchestrates the soft deletion of a retail branch.
/// Enforces domain invariants preventing operations on already-deleted branches and locks future modifications.
/// Audit metadata (DeletedBy, DeletedAt) is populated automatically via AuditSaveChangesInterceptor.
/// </summary>
public sealed class DeleteBranchCommandHandler(
    IBranchRepository _branchRepository,
    IUnitOfWork _unitOfWork) : ICommandHandler<DeleteBranchCommand>
{
    private readonly IBranchRepository _branchRepository = _branchRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result> Handle(
        DeleteBranchCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
        {
            return Result.Failure(BranchErrors.NotFound);
        }

        var deleteResult = branch.SoftDelete();
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
