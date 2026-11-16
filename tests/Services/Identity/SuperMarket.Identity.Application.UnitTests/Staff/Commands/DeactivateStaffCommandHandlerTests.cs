using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Application.Staff.Commands.DeactivateStaff;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.UnitTests.Staff.Commands;

public sealed class DeactivateStaffCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IIdentityProviderService> _identityProviderServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<DeactivateStaffCommandHandler>> _loggerMock = new();

    private readonly DeactivateStaffCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly User _user;

    public DeactivateStaffCommandHandlerTests()
    {
        _handler = new DeactivateStaffCommandHandler(
            _userRepositoryMock.Object,
            _identityProviderServiceMock.Object,
            _permissionServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);

        _user = User.Create(
            username: "walid_staff",
            phoneNumber: "+966501112233",
            keycloakUserId: "kc-user-uuid-12345",
            fullName: "Walid Staff",
            roleId: _roleId).Value;

        _userRepositoryMock
            .Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);

        _identityProviderServiceMock
            .Setup(idp => idp.SetUserActiveStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _identityProviderServiceMock
            .Setup(idp => idp.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _permissionServiceMock
            .Setup(p => p.RevokeUserAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ShouldDeactivateStaff_AndRevokePermissionsAndLogoutKeycloak_WhenValid()
    {
        // Arrange
        var command = new DeactivateStaffCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _user.IsActive.Should().BeFalse();

        _identityProviderServiceMock.Verify(
            idp => idp.SetUserActiveStatusAsync(_user.KeycloakUserId, false, It.IsAny<CancellationToken>()),
            Times.Once);

        _identityProviderServiceMock.Verify(
            idp => idp.LogoutUserAsync(_user.KeycloakUserId, It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _permissionServiceMock.Verify(
            p => p.RevokeUserAccessAsync(_user.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var unknownUserId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(u => u.GetByIdAsync(unknownUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new DeactivateStaffCommand(unknownUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);

        _identityProviderServiceMock.Verify(
            idp => idp.SetUserActiveStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionServiceMock.Verify(
            p => p.RevokeUserAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenKeycloakDeactivationFails()
    {
        // Arrange
        var expectedError = Error.Failure("Keycloak.Error", "Network timeout");
        _identityProviderServiceMock
            .Setup(idp => idp.SetUserActiveStatusAsync(_user.KeycloakUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(expectedError));

        var command = new DeactivateStaffCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
        _user.IsActive.Should().BeTrue(); // Domain invariant preserved

        _identityProviderServiceMock.Verify(
            idp => idp.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionServiceMock.Verify(
            p => p.RevokeUserAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldProceedAndSaveChanges_WhenKeycloakLogoutFails()
    {
        // Arrange
        _identityProviderServiceMock
            .Setup(idp => idp.LogoutUserAsync(_user.KeycloakUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("Keycloak.LogoutFailed", "Transient failure")));

        var command = new DeactivateStaffCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _user.IsActive.Should().BeFalse();

        _unitOfWorkMock.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _permissionServiceMock.Verify(
            p => p.RevokeUserAccessAsync(_user.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate Keycloak sessions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProceedAndSaveChanges_WhenKeycloakLogoutThrowsException()
    {
        // Arrange
        _identityProviderServiceMock
            .Setup(idp => idp.LogoutUserAsync(_user.KeycloakUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Keycloak connection dropped"));

        var command = new DeactivateStaffCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _user.IsActive.Should().BeFalse();

        _unitOfWorkMock.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _permissionServiceMock.Verify(
            p => p.RevokeUserAccessAsync(_user.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error while invalidating Keycloak sessions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
