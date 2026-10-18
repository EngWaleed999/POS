// Handles setting and cryptographically securing a cashier/staff member's PIN.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Cryptography;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.SetStaffPin;

public sealed class SetStaffPinCommandHandler : ICommandHandler<SetStaffPinCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPinHasher _pinHasher;
    private readonly IUnitOfWork _unitOfWork;

    public SetStaffPinCommandHandler(
        IUserRepository userRepository,
        IPinHasher pinHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _pinHasher = pinHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetStaffPinCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        var pinHash = _pinHasher.HashPin(command.Pin);
        var setResult = user.SetPin(pinHash);
        if (setResult.IsFailure)
        {
            return setResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
