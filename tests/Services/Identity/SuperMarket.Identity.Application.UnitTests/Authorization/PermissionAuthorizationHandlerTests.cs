using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuperMarket.Identity.API.Authorization;
using SuperMarket.Identity.Application.Abstractions.Authorization;

namespace SuperMarket.Identity.Application.UnitTests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<PermissionAuthorizationHandler>> _loggerMock = new();
    private readonly PermissionAuthorizationHandler _handler;

    public PermissionAuthorizationHandlerTests()
    {
        _handler = new PermissionAuthorizationHandler(_permissionServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotSucceed_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
        var requirement = new PermissionAuthorizationRequirement("users:create:all");
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        _permissionServiceMock.Verify(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotSucceed_WhenUserIdClaimIsMissing()
    {
        // Arrange
        var identity = new ClaimsIdentity("TestAuth"); // Authenticated without claims
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionAuthorizationRequirement("users:create:all");
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        _permissionServiceMock.Verify(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotSucceed_WhenUserIdClaimIsInvalidGuid()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("sub", "invalid-guid")], "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionAuthorizationRequirement("users:create:all");
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        _permissionServiceMock.Verify(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotSucceed_WhenUserLacksRequiredPermission()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionAuthorizationRequirement("users:create:all");
        var context = new AuthorizationHandlerContext([requirement], user, null);

        _permissionServiceMock
            .Setup(x => x.HasPermissionAsync(userId, "users:create:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldSucceed_WhenUserHasRequiredPermission()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionAuthorizationRequirement("users:create:all");
        var context = new AuthorizationHandlerContext([requirement], user, null);

        _permissionServiceMock
            .Setup(x => x.HasPermissionAsync(userId, "users:create:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PolicyProvider_ShouldReturnDynamicPolicy_WhenPolicyNameStartsWithPermissionPrefix()
    {
        // Arrange
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionAuthorizationPolicyProvider(options);

        // Act
        var policy = await provider.GetPolicyAsync("Permission:users:create:all");

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is PermissionAuthorizationRequirement && ((PermissionAuthorizationRequirement)r).Permission == "users:create:all");
    }

    [Fact]
    public async Task PolicyProvider_ShouldDelegateToFallback_WhenPolicyNameDoesNotStartWithPermissionPrefix()
    {
        // Arrange
        var options = Options.Create(new AuthorizationOptions());
        options.Value.AddPolicy("CustomPolicy", b => b.RequireRole("Admin"));
        var provider = new PermissionAuthorizationPolicyProvider(options);

        // Act
        var policy = await provider.GetPolicyAsync("CustomPolicy");

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().NotBeEmpty();
    }
}
