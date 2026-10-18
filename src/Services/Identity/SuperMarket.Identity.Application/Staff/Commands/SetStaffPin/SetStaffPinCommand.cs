// Command to set or update the fast-login PIN for a cashier or staff member.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.SetStaffPin;

public sealed record SetStaffPinCommand(Guid UserId, string Pin) : ICommand;
