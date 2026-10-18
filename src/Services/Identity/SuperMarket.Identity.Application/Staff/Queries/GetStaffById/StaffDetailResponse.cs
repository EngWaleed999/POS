// Read model representing complete staff member profile details.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Queries.GetStaffById;

public sealed record StaffDetailResponse(
    Guid Id,
    string Username,
    string FullName,
    string PhoneNumber,
    string? Email,
    string KeycloakUserId,
    bool HasPin,
    Guid RoleId,
    string RoleName,
    Guid? BranchId,
    string? BranchName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLogin) : IResponseDto;
