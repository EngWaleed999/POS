using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Application.Branches.Commands.UpdateBranch;

/// <summary>
/// Orchestrates the update of branch profile details, physical address, and operating hours.
/// Fetches the full aggregate to guarantee domain invariant verification, then commits via Unit of Work.
/// </summary>
public sealed class UpdateBranchCommandHandler(
    IBranchRepository _branchRepository,
    IUnitOfWork _unitOfWork) : ICommandHandler<UpdateBranchCommand>
{
    private readonly IBranchRepository _branchRepository = _branchRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result> Handle(
        UpdateBranchCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Fetch full aggregate root with children
        var branch = await _branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
        {
            return Result.Failure(BranchErrors.NotFound);
        }

        // 2. Construct Domain Value Object for Address
        var addressResult = Address.Create(
            street: command.Street,
            city: command.City,
            region: command.Region,
            postalCode: command.PostalCode);

        if (addressResult.IsFailure)
        {
            return Result.Failure(addressResult.Error);
        }

        // 3. Update Address via Domain Method (verifies not deleted)
        var updateAddressResult = branch.UpdateAddress(addressResult.Value);
        if (updateAddressResult.IsFailure)
        {
            return updateAddressResult;
        }

        // 4. Update Core Details via Domain Method (verifies not deleted & non-empty strings)
        var updateDetailsResult = branch.UpdateDetails(
            name: command.Name,
            phone: command.Phone,
            taxNumber: command.TaxNumber,
            email: command.Email);

        if (updateDetailsResult.IsFailure)
        {
            return updateDetailsResult;
        }

        // 5. Update Operating Hours if provided
        if (command.OperatingHours is not null && command.OperatingHours.Count > 0)
        {
            foreach (var hour in command.OperatingHours)
            {
                var hourResult = branch.AddOrUpdateOperatingHour(
                    dayOfWeek: hour.DayOfWeek,
                    openTime: hour.OpenTime,
                    closeTime: hour.CloseTime,
                    isClosed: hour.IsClosed);

                if (hourResult.IsFailure)
                {
                    return hourResult;
                }
            }
        }

        // 6. Commit atomic transaction through tracked changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
