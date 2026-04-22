namespace InvoiceBridge.Domain.Enums;

public enum FileImportStatus
{
    Pending = 1, // Legacy value retained for compatibility with existing records.
    Queued = 1,
    Processing = 2,
    Failed = 3,
    Completed = 4,
    QueuedForRetry = 5,
    Validated = 6 // Legacy value retained for compatibility with existing records.
}
