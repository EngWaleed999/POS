// Production-grade Keycloak Identity Provider implementation with in-memory token caching and REST Admin integration.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Infrastructure.Authentication;

public sealed class KeycloakIdentityProviderService : IIdentityProviderService
{
    private const string AdminTokenCacheKey = "Keycloak_Admin_AccessToken";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakIdentityProviderService> _logger;

    public KeycloakIdentityProviderService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakIdentityProviderService> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<Result<string>> CreateUserAsync(
        string username,
        string? email,
        string phoneNumber,
        string fullName,
        string initialPassword,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<string>(tokenResult.Error);
        }

        var splitNames = SplitFullName(fullName);

        var payload = new
        {
            username = username.Trim(),
            email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            firstName = splitNames.FirstName,
            lastName = splitNames.LastName,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = initialPassword,
                    temporary = false
                }
            },
            attributes = new Dictionary<string, string[]>
            {
                ["phoneNumber"] = [phoneNumber.Trim()]
            }
        };

        var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{_options.Realm}/users")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var location = response.Headers.Location;
                var keycloakUserId = location?.Segments.LastOrDefault()?.Trim('/');

                if (string.IsNullOrWhiteSpace(keycloakUserId))
                {
                    _logger.LogError("Keycloak created user but Location header was missing or invalid.");
                    return Result.Failure<string>(UserErrors.IdentityProviderFailure);
                }

                _logger.LogInformation("Keycloak user created successfully. UserId: {UserId}", keycloakUserId);
                return Result.Success(keycloakUserId);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning("Keycloak conflict while creating user {Username}", username);
                return Result.Failure<string>(UserErrors.UsernameAlreadyExists);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Keycloak user creation failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            return Result.Failure<string>(UserErrors.IdentityProviderFailure);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with Keycloak while creating user {Username}", username);
            return Result.Failure<string>(UserErrors.IdentityProviderFailure);
        }
    }

    public async Task<Result> AssignRoleAsync(
        string keycloakUserId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        // 1. Fetch Role representation by name
        using var getRoleRequest = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(roleName)}");
        getRoleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        using var getRoleResponse = await _httpClient.SendAsync(getRoleRequest, cancellationToken);
        if (!getRoleResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Role {RoleName} not found in Keycloak realm {_Realm}", roleName, _options.Realm);
            return Result.Failure(UserErrors.RoleNotFound);
        }

        var roleJson = await getRoleResponse.Content.ReadAsStringAsync(cancellationToken);
        var roleRepresentation = JsonSerializer.Deserialize<KeycloakRoleRepresentation>(roleJson, JsonOptions);
        if (roleRepresentation is null)
        {
            return Result.Failure(UserErrors.RoleNotFound);
        }

        // 2. Assign Role mapping to User
        var rolesPayload = JsonSerializer.Serialize(new[] { roleRepresentation }, JsonOptions);
        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{_options.Realm}/users/{keycloakUserId}/role-mappings/realm")
        {
            Content = new StringContent(rolesPayload, Encoding.UTF8, "application/json")
        };
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        using var assignResponse = await _httpClient.SendAsync(assignRequest, cancellationToken);
        if (!assignResponse.IsSuccessStatusCode)
        {
            var error = await assignResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to assign role {RoleName} to Keycloak user {UserId}: {Error}", roleName, keycloakUserId, error);
            return Result.Failure(UserErrors.IdentityProviderFailure);
        }

        _logger.LogInformation("Assigned role {RoleName} to Keycloak user {UserId}", roleName, keycloakUserId);
        return Result.Success();
    }

    public async Task<Result> SetUserActiveStatusAsync(
        string keycloakUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        var payload = JsonSerializer.Serialize(new { enabled = isActive }, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"admin/realms/{_options.Realm}/users/{keycloakUserId}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to set active status {IsActive} for Keycloak user {UserId}: {Error}", isActive, keycloakUserId, error);
            return Result.Failure(UserErrors.IdentityProviderFailure);
        }

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"admin/realms/{_options.Realm}/users/{keycloakUserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to delete Keycloak user {UserId}: {Error}", keycloakUserId, error);
            return Result.Failure(UserErrors.IdentityProviderFailure);
        }

        _logger.LogWarning("Deleted Keycloak user {UserId} via compensating transaction", keycloakUserId);
        return Result.Success();
    }

    public async Task<Result<string>> GenerateUserTokenAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<string>(tokenResult.Error);
        }

        // OAuth2 Token Exchange (RFC 8693) to mint user-specific token
        var formParams = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["client_id"] = _options.AdminClientId,
            ["requested_subject"] = keycloakUserId,
            ["subject_token"] = tokenResult.Value
        };

        if (!string.IsNullOrWhiteSpace(_options.AdminClientSecret))
        {
            formParams["client_secret"] = _options.AdminClientSecret;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"realms/{_options.Realm}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(formParams)
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonSerializer.Deserialize<KeycloakTokenResponse>(responseContent, JsonOptions);
                if (tokenResponse?.AccessToken is not null)
                {
                    return Result.Success(tokenResponse.AccessToken);
                }
            }

            _logger.LogError("Keycloak token exchange failed with status {StatusCode}: {ErrorBody}", response.StatusCode, responseContent);
            return Result.Failure<string>(UserErrors.IdentityProviderFailure);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to exchange token for user {UserId}", keycloakUserId);
            return Result.Failure<string>(UserErrors.IdentityProviderFailure);
        }
    }

    // -------------------------------------------------------------------------
    // Admin Access Token Caching (In-Memory with Thread-Safe Lock)
    // -------------------------------------------------------------------------
    private async Task<Result<string>> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(AdminTokenCacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return Result.Success(cachedToken);
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(AdminTokenCacheKey, out cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                return Result.Success(cachedToken);
            }

            var tokenEndpoint = string.IsNullOrWhiteSpace(_options.AdminClientSecret)
                ? "realms/master/protocol/openid-connect/token"
                : $"realms/{_options.Realm}/protocol/openid-connect/token";

            var formParams = new Dictionary<string, string>
            {
                ["client_id"] = _options.AdminClientId
            };

            if (string.IsNullOrWhiteSpace(_options.AdminClientSecret))
            {
                formParams["grant_type"] = "password";
                formParams["username"] = _options.AdminUsername;
                formParams["password"] = _options.AdminPassword;
            }
            else
            {
                formParams["grant_type"] = "client_credentials";
                formParams["client_secret"] = _options.AdminClientSecret;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(formParams)
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to obtain Keycloak admin token. Status: {StatusCode}, Body: {Body}", response.StatusCode, content);
                return Result.Failure<string>(UserErrors.IdentityProviderFailure);
            }

            var tokenData = JsonSerializer.Deserialize<KeycloakTokenResponse>(content, JsonOptions);
            if (tokenData?.AccessToken is null)
            {
                _logger.LogError("Keycloak token response did not contain access_token.");
                return Result.Failure<string>(UserErrors.IdentityProviderFailure);
            }

            // Cache token safely for (expiresIn - 60s) to avoid edge-of-expiry rejections
            var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, tokenData.ExpiresIn - 60));
            _memoryCache.Set(AdminTokenCacheKey, tokenData.AccessToken, cacheDuration);

            _logger.LogInformation("Acquired new Keycloak Admin token. Cached for {Duration} seconds.", cacheDuration.TotalSeconds);
            return Result.Success(tokenData.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error acquiring Keycloak Admin token.");
            return Result.Failure<string>(UserErrors.IdentityProviderFailure);
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        var spaceIndex = trimmed.IndexOf(' ');

        if (spaceIndex <= 0)
        {
            return (trimmed, trimmed);
        }

        var first = trimmed[..spaceIndex];
        var last = trimmed[(spaceIndex + 1)..].Trim();

        return (first, string.IsNullOrWhiteSpace(last) ? first : last);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record KeycloakRoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);
}
