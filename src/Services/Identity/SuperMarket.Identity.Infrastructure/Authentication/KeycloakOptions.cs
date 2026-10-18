// Strongly typed configuration options for Keycloak IAM server integration.

namespace SuperMarket.Identity.Infrastructure.Authentication;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "supermarket";
    public string AdminClientId { get; set; } = "admin-cli";
    public string? AdminClientSecret { get; set; }
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin";
}
