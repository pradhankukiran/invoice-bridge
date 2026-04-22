using FluentValidation;
using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Validation;

public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    private static readonly string[] AllowedMethods =
    [
        "BankTransfer", "ACH", "Wire", "Check", "Card", "CreditNote", "Manual"
    ];

    public RecordPaymentRequestValidator()
    {
        RuleFor(r => r.InvoiceId)
            .GreaterThan(0).WithMessage("InvoiceId must be positive.");

        RuleFor(r => r.Amount)
            .GreaterThan(0m).WithMessage("Payment amount must be positive.")
            .LessThan(1_000_000_000m).WithMessage("Payment amount exceeds maximum allowed.");

        RuleFor(r => r.PaymentDate)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("Payment date cannot be in the future.");

        RuleFor(r => r.Method)
            .NotEmpty()
            .Must(method => AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Payment method must be one of: {string.Join(", ", AllowedMethods)}.");

        RuleFor(r => r.ReferenceNumber)
            .NotEmpty().WithMessage("Reference number is required.")
            .MaximumLength(64);

        RuleFor(r => r.RecordedBy)
            .NotEmpty().WithMessage("RecordedBy is required.")
            .MaximumLength(128);
    }
}
