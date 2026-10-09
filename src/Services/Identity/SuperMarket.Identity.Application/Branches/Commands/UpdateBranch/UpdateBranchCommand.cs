using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.UpdateBranch;

/// <summary>
/// Command to update core operational and contact details of an existing physical supermarket branch.
/// Branch Code is intentionally immutable to safeguard historical auditability and retail identity.
/// </summary>
public sealed record UpdateBranchCommand(
    Guid BranchId,
    string Name,
    string Street,
    string City,
    string Region,
    string? PostalCode,
    string Phone,
    string TaxNumber,
    string? Email,
    IReadOnlyList<UpdateOperatingHoursDto>? OperatingHours = null) : ICommand;
