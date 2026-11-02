using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Application.Staff.Commands.ChangeStaffRole;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.UnitTests.Staff.Commands;

public sealed class ChangeStaffRoleCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IIdentityProviderService> _identityProviderServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ChangeStaffRoleCommandHandler>> _loggerMock = new();

    private readonly ChangeStaffRoleCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _cashierRoleId = Guid.NewGuid();
    private readonly Guid _supervisorRoleId = Guid.NewGuid();
    private readonly Role _cashierRole;
    private readonly Role _supervisorRole;
    private readonly User _user;

    public ChangeStaffRoleCommandHandlerTests()
    {
        _handler = new ChangeStaffRoleCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _identityProviderServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);

        _cashierRole = Role.Create("Cashier", "Cashier staff").Value;
        _supervisorRole = Role.Create("Supervisor", "Store supervisor").Value;

        _user = User.Create(
            username: "walid_staff",
            phoneNumber: "+966501112233",
            keycloakUserId: "kc-user-uuid-12345",
            fullName: "Walid Staff",
            roleId: _cashierRole.Id).Value;

        _userRepositoryMock
            .Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);

        _roleRepositoryMock
            .Setup(r => r.GetByIdAsync(_cashierRole.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cashierRole);

        _roleRepositoryMock
            .Setup(r => r.GetByIdAsync(_supervisorRole.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_supervisorRole);

        _identityProviderServiceMock
            .Setup(idp => idp.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _identityProviderServiceMock
            .Setup(idp => idp.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _userRepositoryMock
            .Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ChangeStaffRoleCommand(_userId, _supervisorRoleId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
        _identityProviderServiceMock.Verify(x => x.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _identityProviderServiceMock.Verify(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnRoleNotFound_WhenNewRoleDoesNotExist()
    {
        // Arrange
        var nonExistentRoleId = Guid.NewGuid();
        _roleRepositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var command = new ChangeStaffRoleCommand(_userId, nonExistentRoleId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.RoleNotFound);
        _identityProviderServiceMock.Verify(x => x.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _identityProviderServiceMock.Verify(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessImmediately_WhenUserAlreadyHasTheNewRole()
    {
        // Arrange: User already has cashier role, requesting cashier role
        var command = new ChangeStaffRoleCommand(_userId, _cashierRole.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _identityProviderServiceMock.Verify(x => x.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _identityProviderServiceMock.Verify(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCallReplaceRoleAsyncWithOldAndNewRole_WhenRoleIsChanged()
    {
        // Arrange: Changing from Cashier to Supervisor
        var command = new ChangeStaffRoleCommand(_userId, _supervisorRole.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verifies old role "Cashier" is replaced with new role "Supervisor" in Keycloak
        _identityProviderServiceMock.Verify(
            x => x.ReplaceRoleAsync("kc-user-uuid-12345", "Cashier", "Supervisor", It.IsAny<CancellationToken>()),
            Times.Once);

        // Verifies all active user sessions are invalidated in Keycloak
        _identityProviderServiceMock.Verify(
            x => x.LogoutUserAsync("kc-user-uuid-12345", It.IsAny<CancellationToken>()),
            Times.Once);

        _user.RoleId.Should().Be(_supervisorRole.Id);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotUpdateUserOrSaveChanges_WhenKeycloakReplaceFails()
    {
        // Arrange: Keycloak network outage or error
        _identityProviderServiceMock
            .Setup(x => x.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(UserErrors.IdentityProviderFailure));

        var command = new ChangeStaffRoleCommand(_userId, _supervisorRole.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.IdentityProviderFailure);

        // User role must NOT be modified in PostgreSQL if IDP sync failed!
        _user.RoleId.Should().Be(_cashierRole.Id);
        _identityProviderServiceMock.Verify(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldProceedAndSaveChanges_WhenKeycloakLogoutFails()
    {
        // Arrange: ReplaceRoleAsync succeeds, but LogoutUserAsync returns failure
        _identityProviderServiceMock
            .Setup(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(UserErrors.IdentityProviderFailure));

        var command = new ChangeStaffRoleCommand(_userId, _supervisorRole.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _user.RoleId.Should().Be(_supervisorRole.Id);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _identityProviderServiceMock.Verify(
            x => x.LogoutUserAsync("kc-user-uuid-12345", It.IsAny<CancellationToken>()),
            Times.Once);

        // Structured warning must be logged without failing the overall role update
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate active sessions in Keycloak")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProceedAndSaveChanges_WhenKeycloakLogoutThrowsException()
    {
        // Arrange: ReplaceRoleAsync succeeds, but LogoutUserAsync throws unexpected exception
        var networkException = new HttpRequestException("Keycloak connection timeout");
        _identityProviderServiceMock
            .Setup(x => x.LogoutUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(networkException);

        var command = new ChangeStaffRoleCommand(_userId, _supervisorRole.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _user.RoleId.Should().Be(_supervisorRole.Id);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _identityProviderServiceMock.Verify(
            x => x.LogoutUserAsync("kc-user-uuid-12345", It.IsAny<CancellationToken>()),
            Times.Once);

        // Structured warning with exception must be logged without failing the overall role update
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error while invalidating Keycloak sessions")),
                networkException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
