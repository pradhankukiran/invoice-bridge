using System.ComponentModel.DataAnnotations;

namespace InvoiceBridge.Web.Workers;

public sealed class WorkerOptions
{
    public const string SectionName = "Workers";

    public ImportQueueWorkerOptions ImportQueue { get; set; } = new();
    public ApprovalSlaWorkerOptions ApprovalSla { get; set; } = new();
    public NotificationOutboxWorkerOptions NotificationOutbox { get; set; } = new();
}

public sealed class ImportQueueWorkerOptions
{
    public bool Enabled { get; set; } = true;

    [Range(5, 3600)]
    public int PollIntervalSeconds { get; set; } = 15;

    [Range(1, 100)]
    public int BatchSize { get; set; } = 10;

    public string ProcessedBy { get; set; } = "import.worker";
}

public sealed class ApprovalSlaWorkerOptions
{
    public bool Enabled { get; set; } = true;

    [Range(10, 3600)]
    public int PollIntervalSeconds { get; set; } = 60;

    [Range(5, 168)]
    public int WarningThresholdHours { get; set; } = 24;

    [Range(5, 336)]
    public int BreachThresholdHours { get; set; } = 48;
}

public sealed class NotificationOutboxWorkerOptions
{
    public bool Enabled { get; set; } = true;

    [Range(5, 3600)]
    public int PollIntervalSeconds { get; set; } = 10;

    [Range(1, 100)]
    public int BatchSize { get; set; } = 25;
}
