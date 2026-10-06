using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Application.Branches.Commands.CreateBranch;

/// <summary>
/// Orchestrates the creation of a physical retail supermarket branch.
/// Enforces business uniqueness, constructs domain primitives, and commits via Unit of Work.
/// </summary>
public sealed class CreateBranchCommandHandler(IBranchRepository _branchRepository,IUnitOfWork _unitOfWork) 
: ICommandHandler<CreateBranchCommand, Guid>
{
    private readonly IBranchRepository _branchRepository = _branchRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result<Guid>> Handle(
        CreateBranchCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Construct Domain Value Objects
        var codeResult = BranchCode.Create(command.Code);
        if (codeResult.IsFailure)
        {
            return Result.Failure<Guid>(codeResult.Error);
        }

        var addressResult = Address.Create(
            command.Street,
            command.City,
            command.Region,
            command.PostalCode);

        if (addressResult.IsFailure)
        {
            return Result.Failure<Guid>(addressResult.Error);
        }

        // 2. Enforce Business Invariant: Unique Branch Code
        var codeExists = await _branchRepository.ExistsByCodeAsync(codeResult.Value, cancellationToken);
        if (codeExists)
        {
            return Result.Failure<Guid>(BranchErrors.CodeAlreadyExists);
        }

        // 3. Construct Aggregate Root via Factory Method
        var branchResult = Branch.Create(
            code: codeResult.Value,
            name: command.Name,
            address: addressResult.Value,
            phone: command.Phone,
            taxNumber: command.TaxNumber,
            email: command.Email,
            currency: command.Currency);

        if (branchResult.IsFailure)
        {
            return Result.Failure<Guid>(branchResult.Error);
        }

        var branch = branchResult.Value;

        // 4. Register entity in In-Memory ChangeTracker
        _branchRepository.Add(branch);

        // 5. Commit atomic transaction to database & dispatch queued domain events
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Return persistent Guid identifier
        return Result.Success(branch.Id);
    }
}
