using FluentValidation;
using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Validation;

public sealed class CreateAccountingExportRequestValidator : AbstractValidator<CreateAccountingExportRequest>
{
    private static readonly string[] AllowedFormats = ["CSV", "XML"];

    public CreateAccountingExportRequestValidator()
    {
        RuleFor(r => r.GeneratedBy)
            .NotEmpty().WithMessage("GeneratedBy is required.")
            .MaximumLength(128);

        RuleFor(r => r.Format)
            .NotEmpty()
            .Must(format => AllowedFormats.Contains(format.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Format must be one of: {string.Join(", ", AllowedFormats)}.");

        RuleFor(r => r.InvoiceIds)
            .Must((request, ids) => request.IncludeAllEligible || (ids is { Count: > 0 }))
            .WithMessage("Provide InvoiceIds when IncludeAllEligible is false.");

        RuleForEach(r => r.InvoiceIds)
            .GreaterThan(0).WithMessage("Invoice ids must be positive.");
    }
}
