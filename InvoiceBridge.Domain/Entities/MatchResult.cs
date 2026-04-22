using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class MatchResult
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public MatchResultCode ResultCode { get; set; }
    public bool IsMatch { get; set; }
    public DateTimeOffset ExecutedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string ExecutedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<MatchResultLine> Lines { get; set; } = new List<MatchResultLine>();
}
