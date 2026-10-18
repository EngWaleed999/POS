// Command to authenticate a cashier using their username and PIN code at a specific POS register.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Auth.Commands.LoginWithPin;

public sealed record LoginWithPinCommand(
    string Username,
    string Pin,
    Guid RegisterId) : ICommand<CashierLoginResponse>;
