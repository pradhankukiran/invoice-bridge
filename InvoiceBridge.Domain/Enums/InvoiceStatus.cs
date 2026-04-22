namespace InvoiceBridge.Domain.Enums;

public enum InvoiceStatus
{
    Imported = 1,
    Matched = 2,
    Exception = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    ReadyToPay = 7,
    Paid = 8
}
