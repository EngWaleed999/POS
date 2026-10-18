// Command to register a new staff member or cashier account.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.RegisterStaff;

public sealed record RegisterStaffCommand(
    string Username,
    string FullName,
    string PhoneNumber,
    string? Email,
    Guid RoleId,
    Guid? BranchId,
    string InitialPassword) : ICommand<Guid>;
