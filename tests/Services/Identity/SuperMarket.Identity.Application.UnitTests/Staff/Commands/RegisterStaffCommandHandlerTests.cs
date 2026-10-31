// Production-grade tests demonstrating dual-write consistency, partial failures, and race condition hazards in Staff Registration.

using FluentAssertions;
using Moq;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Application.Staff.Commands.RegisterStaff;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Application.UnitTests.Staff.Commands;

public sealed class RegisterStaffCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IBranchRepository> _branchRepositoryMock = new();
    private readonly Mock<IIdentityProviderService> _identityProviderServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly RegisterStaffCommandHandler _handler;

    private readonly Guid _validRoleId = Guid.NewGuid();
    private readonly Guid _validBranchId = Guid.NewGuid();
    private readonly Role _cashierRole;
    private readonly Branch _activeBranch;

    public RegisterStaffCommandHandlerTests()
    {
        _handler = new RegisterStaffCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _branchRepositoryMock.Object,
            _identityProviderServiceMock.Object,
            _unitOfWorkMock.Object);

        // Setup valid Role and Branch entities
        _cashierRole = Role.Create("Cashier", "Standard front-desk cashier").Value;
        
        var branchCode = BranchCode.Create("BR-01").Value;
        var address = Address.Create("King Fahd Road", "Riyadh", "Central", "12345").Value;
        _activeBranch = Branch.Create(branchCode, "Main Branch", address, "+966500000000", "300000000000003").Value;

        _roleRepositoryMock
            .Setup(r => r.GetByIdAsync(_validRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cashierRole);

        _branchRepositoryMock
            .Setup(b => b.GetByIdAsync(_validBranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeBranch);

        _userRepositoryMock
            .Setup(u => u.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(u => u.ExistsByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private RegisterStaffCommand CreateSampleCommand(string username = "saleh.cashier") =>
        new(
            Username: username,
            FullName: "Saleh Al-Harbi",
            PhoneNumber: "+966512345678",
            Email: "saleh@supermarket.com",
            RoleId: _validRoleId,
            BranchId: _validBranchId,
            InitialPassword: "SecurePassword123!");

    // -------------------------------------------------------------------------
    // 1. Happy Path Baseline
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Handle_ShouldSucceed_WhenAllServicesAreHealthy()
    {
        // Arrange
        var command = CreateSampleCommand();
        const string keycloakId = "kc-uuid-12345";

        _identityProviderServiceMock
            .Setup(idp => idp.CreateUserAsync(
                command.Username,
                command.Email,
                command.PhoneNumber,
                command.FullName,
                command.InitialPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(keycloakId));

        _identityProviderServiceMock
            .Setup(idp => idp.AssignRoleAsync(keycloakId, _cashierRole.RoleName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _userRepositoryMock.Verify(repo => repo.Add(It.Is<User>(u => u.KeycloakUserId == keycloakId)), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _identityProviderServiceMock.Verify(idp => idp.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // 2. Fault Injection: Partial Failure (Database throws after Keycloak succeeds)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Handle_ShouldTriggerCompensatingDelete_WhenDatabaseSaveFails()
    {
        // Scenario:
        // Keycloak created the user successfully.
        // But PostgreSQL connection timed out or database failed on SaveChangesAsync.
        // Handler should catch the DB error and attempt to delete the user from Keycloak.

        // Arrange
        var command = CreateSampleCommand();
        const string keycloakId = "kc-uuid-failed-db";

        _identityProviderServiceMock
            .Setup(idp => idp.CreateUserAsync(
                command.Username,
                command.Email,
                command.PhoneNumber,
                command.FullName,
                command.InitialPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(keycloakId));

        _identityProviderServiceMock
            .Setup(idp => idp.AssignRoleAsync(keycloakId, _cashierRole.RoleName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Injected Fault: Database fails with a connection exception
        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timed out during commit."));

        _identityProviderServiceMock
            .Setup(idp => idp.DeleteUserAsync(keycloakId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert:
        // The handler rethrows the exception, but must call DeleteUserAsync to clean up Keycloak
        await act.Should().ThrowAsync<TimeoutException>();

        _identityProviderServiceMock.Verify(
            idp => idp.DeleteUserAsync(keycloakId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Compensating transaction must be invoked to avoid an orphaned account in Keycloak.");
    }

    // -------------------------------------------------------------------------
    // 3. Double-Fault: The Production Nightmare (DB Fails + Compensation Fails)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Handle_ExposesDoubleFaultHazard_WhenDatabaseFailsAndKeycloakCompensationAlsoFails()
    {
        // Scenario:
        // 1. User created in Keycloak (ID: kc-uuid-orphan).
        // 2. Database fails (e.g. disk full or deadlock).
        // 3. The catch block executes `DeleteUserAsync`.
        // 4. BUT Keycloak REST API is also unreachable or returns 503 / Network Exception!
        // RESULT: The user exists in Keycloak forever, but does NOT exist in PostgreSQL.

        // Arrange
        var command = CreateSampleCommand();
        const string keycloakId = "kc-uuid-orphan";

        _identityProviderServiceMock
            .Setup(idp => idp.CreateUserAsync(
                command.Username,
                command.Email,
                command.PhoneNumber,
                command.FullName,
                command.InitialPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(keycloakId));

        _identityProviderServiceMock
            .Setup(idp => idp.AssignRoleAsync(keycloakId, _cashierRole.RoleName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PostgreSQL transaction aborted."));

        // Injected Double Fault: Compensation network call also crashes!
        _identityProviderServiceMock
            .Setup(idp => idp.DeleteUserAsync(keycloakId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Keycloak connection dropped during compensating delete."));

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert:
        // The compensation exception swallows the original database error!
        // Keycloak now contains an orphaned user that can never be recovered automatically.
        var thrownException = await act.Should().ThrowAsync<HttpRequestException>();
        thrownException.WithMessage("*Keycloak connection dropped*");

        // The system is now in an inconsistent distributed state (Dual-Write Desynchronization)
    }

    // -------------------------------------------------------------------------
    // 4. Concurrency Race Hazard: Check-Then-Act Window
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Handle_ExposesCheckThenActHazard_WhenTwoIdenticalRequestsInterleave()
    {
        // Scenario:
        // Two requests (Req A and Req B) arrive simultaneously with username 'saleh.cashier'.
        // Step 1: Both pass `ExistsByUsernameAsync` returning `false` (Check-Then-Act window).
        // Step 2: Both call `CreateUserAsync` in Keycloak.
        // Step 3: Keycloak creates both OR one fails with 409 in Keycloak.
        // If Keycloak creates both, PostgreSQL unique index throws raw DbUpdateException on the second!

        // Arrange
        var commandA = CreateSampleCommand("concurrent.user");
        var commandB = CreateSampleCommand("concurrent.user");

        var barrier = new Barrier(2); // Forces both threads to rendezvous at the same microsecond

        _userRepositoryMock
            .Setup(u => u.ExistsByUsernameAsync("concurrent.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(2));
                return false; // Both see that the user does not exist!
            });

        _identityProviderServiceMock
            .Setup(idp => idp.CreateUserAsync(
                "concurrent.user",
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("kc-uuid-concurrent"));

        _identityProviderServiceMock
            .Setup(idp => idp.AssignRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _unitOfWorkMock
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act: Execute both commands concurrently
        var taskA = Task.Run(() => _handler.Handle(commandA, CancellationToken.None));
        var taskB = Task.Run(() => _handler.Handle(commandB, CancellationToken.None));

        var results = await Task.WhenAll(taskA, taskB);

        // Assert:
        // Both requests passed the application-level uniqueness check!
        // Both attempted to provision the user in Keycloak simultaneously!
        _identityProviderServiceMock.Verify(
            idp => idp.CreateUserAsync("concurrent.user", It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "Both requests bypassed the uniqueness check due to the Check-Then-Act race condition window.");
    }
}
