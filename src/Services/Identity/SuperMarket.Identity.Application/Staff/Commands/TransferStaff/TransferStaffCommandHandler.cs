// Handles staff branch transfer, validating destination branch and publishing domain events.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.TransferStaff;

public sealed class TransferStaffCommandHandler : ICommandHandler<TransferStaffCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransferStaffCommandHandler(
        IUserRepository userRepository,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(TransferStaffCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (command.NewBranchId.HasValue)
        {
            var branch = await _branchRepository.GetByIdAsync(command.NewBranchId.Value, cancellationToken);
            if (branch is null || !branch.IsActive)
            {
                return Result.Failure(UserErrors.BranchNotFound);
            }
        }

        var transferResult = user.AssignToBranch(command.NewBranchId);
        if (transferResult.IsFailure)
        {
            return transferResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
