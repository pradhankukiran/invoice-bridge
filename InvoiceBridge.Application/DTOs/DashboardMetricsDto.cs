namespace InvoiceBridge.Application.DTOs;

public sealed record DashboardMetricsDto(
    int TotalOpenPurchaseOrders,
    int ImportedInvoices,
    int PendingApprovals,
    int ExceptionInvoices,
    int FailedImports,
    int MatchedLast7Days);
