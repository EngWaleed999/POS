# SuperMarket.BuildingBlocks — Testing Strategy & Quality Assurance

> **Scope:** Audit of existing test coverage, testing guidelines, high-priority test suites, and mock recipes for `SuperMarket.BuildingBlocks`.

---

## 1. Current Test Suite Status

* **Test Suite Status:** `Gaps Identified`
* **Current State:** The solution's root `tests/` directory is currently empty. There are no automated unit or integration tests committed specifically for `SuperMarket.BuildingBlocks`.
* **Required Action:** A dedicated test project (e.g. `tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests`) must be created to lock down core invariants and interceptor behaviors.

---

## 2. Test Coverage & Gap Analysis

| Component | Current Coverage | Priority | Key Invariants & Scenarios to Test |
| :--- | :--- | :--- | :--- |
| **`Result` & `Result<TValue>`** | None | **Critical** | Invariant guards (success with error, failure with `Error.None`), `Value` getter exception on failure, implicit operators. |
| **`ResultExtensions` (ROP)** | None | **Critical** | `Match` executes correct branch, `Ensure` flips to failure on false predicate, `Map` transforms values, `Bind` short-circuits. |
| **`Entity<TId>` & `ValueObject`** | None | **High** | Identity equality vs reference equality, transient entities equality behavior, hash code consistency, value object structural equality. |
| **`ValidationPipelineBehavior`** | None | **High** | Passes when no validators registered, executes multiple validators in parallel, aggregates failure messages into `Error.Validation`, returns failure `Result` without calling `next()`. |
| **`PerformancePipelineBehavior`** | None | **High** | Invokes `next()`, measures elapsed time, increments `pos_requests_total`, emits duration metric, logs warning when elapsed > threshold, executes `finally` on crash. |
| **`AuditSaveChangesInterceptor`** | None | **Critical** | Sets `CreatedAt`/`CreatedBy` on added entities, sets `UpdatedAt`/`UpdatedBy` on modified entities, overrides modifications to `CreatedAt`/`CreatedBy`, converts `EntityState.Deleted` to `Modified` with soft-delete flags, verifies mock `TimeProvider`. |
| **`DispatchDomainEventsInterceptor`** | None | **Critical** | Extracts events from `IAggregateRoot`, clears event queue before publish, publishes each event via `IPublisher`, handles empty queues gracefully. |
| **`PagedList<T>` & `CursorPagedList`** | None | **Medium** | Offset page calculations (`TotalPages`, `HasNextPage`, `HasPreviousPage`), defensive parameter clamping, keyset next cursor extraction. |

---

## 3. High-Priority Unit Test Implementation Recipes

### 3.1 Testing `Result` Invariants (xUnit + FluentAssertions)
```csharp
[Fact]
public void Constructor_ShouldThrowInvalidOperationException_WhenSuccessInitializedWithError()
{
    // Act
    Action act = () => new TestResult(isSuccess: true, error: Error.Failure("Code", "Desc"));

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*successful result cannot be initialized with an error*");
}

[Fact]
public void Value_ShouldThrowInvalidOperationException_WhenResultIsFailure()
{
    // Arrange
    Result<string> result = Result.Failure<string>(Error.Validation("Code", "Desc"));

    // Act
    Action act = () => _ = result.Value;

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*The value of a failure result cannot be accessed*");
}
```

### 3.2 Testing `AuditSaveChangesInterceptor` with Mock `TimeProvider`
```csharp
[Fact]
public async Task SavingChangesAsync_ShouldSetUtcTimestampAndActor_ForAddedAuditableEntity()
{
    // Arrange
    var fixedTime = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    var fakeTimeProvider = new FakeTimeProvider(fixedTime);
    var userContextMock = new Mock<ICurrentUserContext>();
    userContextMock.Setup(u => u.UserId).Returns("USER_123");

    var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, fakeTimeProvider);

    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .AddInterceptors(interceptor)
        .Options;

    using var context = new TestDbContext(options);
    var entity = new TestAuditableEntity();
    context.Add(entity);

    // Act
    await context.SaveChangesAsync();

    // Assert
    entity.CreatedAt.Should().Be(fixedTime);
    entity.CreatedBy.Should().Be("USER_123");
}
```

### 3.3 Testing `DispatchDomainEventsInterceptor` Idempotency
```csharp
[Fact]
public async Task SavingChangesAsync_ShouldClearEventsBeforeDispatching_ToPreventDuplicatePublishing()
{
    // Arrange
    var publisherMock = new Mock<IPublisher>();
    var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);

    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .AddInterceptors(interceptor)
        .Options;

    using var context = new TestDbContext(options);
    var aggregate = new TestAggregate(Guid.NewGuid());
    aggregate.AddTestEvent(new TestDomainEvent());
    context.Add(aggregate);

    // Act
    await context.SaveChangesAsync();

    // Assert
    aggregate.DomainEvents.Should().BeEmpty();
    publisherMock.Verify(p => p.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

---

## 4. Recommended Test Automation Commands

Once the test project is set up:
```bash
# Run all unit tests with concise logger
dotnet test tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests/

# Run with code coverage reporting
dotnet test --collect:"XPlat Code Coverage"
```
