// Response payload returned upon successful cashier fast-login.

namespace SuperMarket.Identity.Application.Auth.Commands.LoginWithPin;

public sealed record CashierLoginResponse(
    string AccessToken,
    int ExpiresInSeconds,
    Guid CashierId,
    string Username,
    string FullName,
    Guid RoleId,
    string RoleName,
    Guid BranchId,
    Guid RegisterId,
    IReadOnlyList<string> Permissions);
