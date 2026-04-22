namespace InvoiceBridge.Domain.Enums;

public enum MatchResultCode
{
    Matched = 1,
    QuantityVariance = 2,
    PriceVariance = 3,
    TaxVariance = 4,
    MissingPurchaseOrderLine = 5,
    MissingGoodsReceipt = 6,
    DuplicateInvoice = 7
}
