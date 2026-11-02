using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Infrastructure.Authentication;

namespace SuperMarket.Identity.Application.UnitTests.Authentication;

public sealed class KeycloakIdentityProviderServiceTests
{
    private const string TestRealm = "test-realm";
    private const string AdminToken = "valid-admin-access-token";
    private const string UserId = "user-uuid-123";

    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<KeycloakOptions> _options;

    public KeycloakIdentityProviderServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _memoryCache.Set("Keycloak_Admin_AccessToken", AdminToken);

        _options = Options.Create(new KeycloakOptions
        {
            BaseUrl = "http://localhost:8080/",
            Realm = TestRealm,
            AdminClientId = "admin-cli"
        });
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.OK)]
    public async Task LogoutUserAsync_ShouldSendAuthenticatedPostAndReturnSuccess_WhenResponseIsSuccessful(
        HttpStatusCode statusCode)
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var handler = new DelegatingTestHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(statusCode);
        });

        using var httpClient = new HttpClient(handler);
        var service = new KeycloakIdentityProviderService(
            httpClient,
            _memoryCache,
            _options,
            NullLogger<KeycloakIdentityProviderService>.Instance);

        // Act
        var result = await service.LogoutUserAsync(UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be($"http://localhost:8080/admin/realms/{TestRealm}/users/{UserId}/logout");
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be(AdminToken);
    }

    [Fact]
    public async Task LogoutUserAsync_ShouldReturnFailure_WhenKeycloakReturnsErrorStatus()
    {
        // Arrange
        var handler = new DelegatingTestHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"Internal Server Error\"}")
        });

        using var httpClient = new HttpClient(handler);
        var service = new KeycloakIdentityProviderService(
            httpClient,
            _memoryCache,
            _options,
            NullLogger<KeycloakIdentityProviderService>.Instance);

        // Act
        var result = await service.LogoutUserAsync(UserId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.IdentityProviderFailure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogoutUserAsync_ShouldReturnFailureImmediately_WhenUserIdIsMissingOrWhitespace(string? invalidUserId)
    {
        // Arrange
        var handler = new DelegatingTestHandler(_ => throw new InvalidOperationException("Should not be called"));

        using var httpClient = new HttpClient(handler);
        var service = new KeycloakIdentityProviderService(
            httpClient,
            _memoryCache,
            _options,
            NullLogger<KeycloakIdentityProviderService>.Instance);

        // Act
        var result = await service.LogoutUserAsync(invalidUserId!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.IdentityProviderFailure);
    }

    [Fact]
    public async Task LogoutUserAsync_ShouldReturnFailure_WhenNetworkExceptionOccurs()
    {
        // Arrange
        var handler = new DelegatingTestHandler(_ => throw new HttpRequestException("Connection refused"));

        using var httpClient = new HttpClient(handler);
        var service = new KeycloakIdentityProviderService(
            httpClient,
            _memoryCache,
            _options,
            NullLogger<KeycloakIdentityProviderService>.Instance);

        // Act
        var result = await service.LogoutUserAsync(UserId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.IdentityProviderFailure);
    }

    private sealed class DelegatingTestHandler(Func<HttpRequestMessage, HttpResponseMessage> sendHandler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(sendHandler(request));
        }
    }
}
