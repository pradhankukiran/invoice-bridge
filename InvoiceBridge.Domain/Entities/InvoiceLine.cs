namespace InvoiceBridge.Domain.Entities;

public sealed class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int LineNumber { get; set; }
    public required string ItemCode { get; set; }
    public required string Description { get; set; }
    public decimal BilledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }

    public ICollection<MatchResultLine> MatchResultLines { get; set; } = new List<MatchResultLine>();
}
