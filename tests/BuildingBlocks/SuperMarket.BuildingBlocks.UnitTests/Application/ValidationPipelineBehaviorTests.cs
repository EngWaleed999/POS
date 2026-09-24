using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/>
/// validating pre-execution validation gates, error aggregation, Result/Result&lt;T&gt; adaptation,
/// and fast short-circuiting when validation fails.
/// </summary>
public class ValidationPipelineBehaviorTests
{
    // =========================================================================
    // 1. No Validators Registered (Bypass / Pass-Through)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldInvokeNextDelegate_WhenNoValidatorsAreRegistered()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<CreateProductCommand>>();
        var behavior = new ValidationPipelineBehavior<CreateProductCommand, Result>(validators);

        var command = new CreateProductCommand("Valid Product", 100m);
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        nextCalled.Should().BeTrue("pipeline must immediately advance to the next delegate when no validators exist");
    }

    // =========================================================================
    // 2. All Validators Pass (Happy Path)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldInvokeNextDelegate_WhenAllValidatorsPass()
    {
        // Arrange
        var validator = new CreateProductCommandValidator();
        var validators = new List<IValidator<CreateProductCommand>> { validator };
        var behavior = new ValidationPipelineBehavior<CreateProductCommand, Result>(validators);

        var validCommand = new CreateProductCommand("Fresh Apples", 15.5m);
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(validCommand, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        nextCalled.Should().BeTrue("pipeline must advance to handler when all validation rules are satisfied");
    }

    // =========================================================================
    // 3. Validation Failure on Result (Non-Generic Result Return Type)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldShortCircuitAndReturnValidationFailure_WhenValidationFailsOnResultCommand()
    {
        // Arrange
        var validator = new CreateProductCommandValidator();
        var validators = new List<IValidator<CreateProductCommand>> { validator };
        var behavior = new ValidationPipelineBehavior<CreateProductCommand, Result>(validators);

        // Invalid: Name is empty, Price is negative (-5)
        var invalidCommand = new CreateProductCommand("", -5m);
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(invalidCommand, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("General.Validation");
        result.Error.Description.Should().Contain("Name: Product name is required");
        result.Error.Description.Should().Contain("Price: Product price must be greater than zero");

        // Verify structured ValidationError dictionary for frontend RFC 7807 problem details
        result.Error.Should().BeOfType<ValidationError>();
        var validationError = (ValidationError)result.Error;
        validationError.Errors.Should().ContainKey("Name");
        validationError.Errors["Name"].Should().Contain("Product name is required");
        validationError.Errors.Should().ContainKey("Price");
        validationError.Errors["Price"].Should().Contain("Product price must be greater than zero");

        nextCalled.Should().BeFalse(
            "handler must NEVER be invoked when validation fails, preventing invalid data from reaching business logic");
    }

    // =========================================================================
    // 4. Validation Failure on Result<T> (Generic Result Return Type via Reflection)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldShortCircuitAndReturnGenericValidationFailure_WhenValidationFailsOnResultTQuery()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<GetProductByIdQuery>>();
        var failures = new List<ValidationFailure>
        {
            new("ProductId", "ProductId must not be empty.")
        };

        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<GetProductByIdQuery>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var validators = new List<IValidator<GetProductByIdQuery>> { validatorMock.Object };
        var behavior = new ValidationPipelineBehavior<GetProductByIdQuery, Result<ProductDto>>(validators);

        var query = new GetProductByIdQuery(Guid.Empty);
        var nextCalled = false;
        RequestHandlerDelegate<Result<ProductDto>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(new ProductDto(Guid.NewGuid(), "Test", 10m)));
        };

        // Act
        var result = await behavior.Handle(query, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("General.Validation");
        result.Error.Description.Should().Contain("ProductId: ProductId must not be empty.");

        // Invariant check: Accessing value on failed Result<T> must throw
        Action act = () => _ = result.Value;
        act.Should().ThrowExactly<InvalidOperationException>();

        nextCalled.Should().BeFalse();
    }

    // =========================================================================
    // 5. Unsupported Return Type (Defensive Exception Trap)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenValidationFailsOnNonResultReturnType()
    {
        // Arrange: A request returning raw string instead of Result or Result<T>
        var validatorMock = new Mock<IValidator<RawStringRequest>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<RawStringRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Text", "Text cannot be blank") }));

        var validators = new List<IValidator<RawStringRequest>> { validatorMock.Object };
        var behavior = new ValidationPipelineBehavior<RawStringRequest, string>(validators);

        var request = new RawStringRequest("");
        RequestHandlerDelegate<string> next = () => Task.FromResult("should not be reached");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*is not a supported Result type in ValidationPipelineBehavior*");
    }

    // =========================================================================
    // 6. Cancellation Token Forwarding
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken_ToAllValidators()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var expectedToken = cts.Token;

        var validatorMock = new Mock<IValidator<CreateProductCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateProductCommand>>(), expectedToken))
            .ReturnsAsync(new ValidationResult());

        var validators = new List<IValidator<CreateProductCommand>> { validatorMock.Object };
        var behavior = new ValidationPipelineBehavior<CreateProductCommand, Result>(validators);

        var command = new CreateProductCommand("Valid Item", 50m);
        RequestHandlerDelegate<Result> next = () => Task.FromResult(Result.Success());

        // Act
        await behavior.Handle(command, next, expectedToken);

        // Assert
        validatorMock.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateProductCommand>>(), expectedToken), Times.Once);
    }

    // =========================================================================
    // Test Fakes & Dtos
    // =========================================================================

    public record CreateProductCommand(string Name, decimal Price) : IRequest<Result>;

    public record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;

    public record RawStringRequest(string Text) : IRequest<string>;

    public record ProductDto(Guid Id, string Name, decimal Price);

    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required");
            RuleFor(x => x.Price).GreaterThan(0).WithMessage("Product price must be greater than zero");
        }
    }
}
