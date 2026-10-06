using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.CreateBranch;

/// <summary>
/// Command to create a new physical supermarket retail branch.
/// Returns the unique Guid of the newly created branch upon success.
/// </summary>
public sealed record CreateBranchCommand(
    string Code,
    string Name,
    string Street,
    string City,
    string Region,
    string? PostalCode,
    string Phone,
    string TaxNumber,
    string? Email,
    string Currency = "YE") : ICommand<Guid>;
