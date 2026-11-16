// Port contract model representing authentication token metadata returned by the Identity Provider.

namespace SuperMarket.Identity.Application.Abstractions.Authentication;

public sealed record IdentityProviderTokenResult(
    string AccessToken,
    int ExpiresIn);
