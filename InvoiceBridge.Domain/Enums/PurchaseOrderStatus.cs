namespace InvoiceBridge.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    PartiallyReceived = 3,
    FullyReceived = 4,
    Closed = 5
}
