using FluentValidation;

namespace SuperMarket.Identity.Application.Branches.Commands.UpdateBranch;

/// <summary>
/// Validator for UpdateBranchCommand ensuring structural integrity, boundary constraints, and schedule validity.
/// Executed automatically via ValidationPipelineBehavior before reaching the handler.
/// </summary>
public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(150).WithMessage("Branch name cannot exceed 150 characters.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street address is required.")
            .MaximumLength(200).WithMessage("Street address cannot exceed 200 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters.");

        RuleFor(x => x.Region)
            .NotEmpty().WithMessage("Region is required.")
            .MaximumLength(100).WithMessage("Region cannot exceed 100 characters.");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PostalCode));

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[0-9\s\-()]{7,25}$").WithMessage("Invalid phone number format.");

        RuleFor(x => x.TaxNumber)
            .NotEmpty().WithMessage("Tax number is required.")
            .MaximumLength(50).WithMessage("Tax number cannot exceed 50 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email address format.")
            .MaximumLength(150).WithMessage("Email cannot exceed 150 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        When(x => x.OperatingHours is not null && x.OperatingHours.Count > 0, () =>
        {
            RuleFor(x => x.OperatingHours)
                .Must(hours => hours!.Select(h => h.DayOfWeek).Distinct().Count() == hours!.Count)
                .WithMessage("Operating hours cannot contain duplicate entries for the same day of the week.");

            RuleForEach(x => x.OperatingHours!).ChildRules(hour =>
            {
                hour.RuleFor(h => h)
                    .Must(h => h.IsClosed || h.OpenTime != h.CloseTime)
                    .WithMessage("Open time and close time cannot be identical unless marked as closed.");
            });
        });
    }
}
