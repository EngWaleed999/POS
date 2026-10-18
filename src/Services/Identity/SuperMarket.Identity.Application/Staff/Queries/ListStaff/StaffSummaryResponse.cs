// Read model representing summarized staff data for administrative listings.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Queries.ListStaff;

public sealed record StaffSummaryResponse(
    Guid Id,
    string Username,
    string FullName,
    string PhoneNumber,
    string? Email,
    Guid RoleId,
    string RoleName,
    Guid? BranchId,
    string? BranchName,
    bool HasPin,
    bool IsActive,
    DateTimeOffset CreatedAt) : IResponseDto;
