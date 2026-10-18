// Minimal API endpoints for cashier and staff authentication sessions.

using MediatR;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Auth.Commands.LoginWithPin;

namespace SuperMarket.Identity.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // --- 1. Cashier Fast-Login via PIN ---
        group.MapPost("/cashier-login", async (CashierLoginRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new LoginWithPinCommand(
                Username: request.Username,
                Pin: request.Pin,
                RegisterId: request.RegisterId);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("CashierLogin")
        .WithSummary("Authenticates a cashier at a specific POS terminal using PIN credentials.")
        .Produces<CashierLoginResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

// --- Request Contract ---

public sealed record CashierLoginRequest(
    string Username,
    string Pin,
    Guid RegisterId);
