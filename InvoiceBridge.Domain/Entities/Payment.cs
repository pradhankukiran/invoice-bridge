namespace InvoiceBridge.Domain.Entities;

public sealed class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string Method { get; set; }
    public required string ReferenceNumber { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
