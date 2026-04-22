using FluentValidation;
using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Validation;

public sealed class InvoiceImportRequestValidator : AbstractValidator<InvoiceImportRequest>
{
    private const int MaxXmlPayloadBytes = 8 * 1024 * 1024;

    public InvoiceImportRequestValidator()
    {
        RuleFor(r => r.XmlContent)
            .NotEmpty().WithMessage("Invoice XML content is required.")
            .Must(content => System.Text.Encoding.UTF8.GetByteCount(content) <= MaxXmlPayloadBytes)
            .WithMessage($"Invoice XML exceeds the {MaxXmlPayloadBytes / (1024 * 1024)}MB limit.");

        RuleFor(r => r.FileName)
            .MaximumLength(260)
            .Matches(@"^[^\\/:*?""<>|\r\n]*$")
            .WithMessage("File name contains invalid path characters.");

        RuleFor(r => r.ImportedBy)
            .NotEmpty().WithMessage("ImportedBy is required.")
            .MaximumLength(128);
    }
}
