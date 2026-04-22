using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> QueueImportAsync(InvoiceImportRequest request, CancellationToken cancellationToken = default);
    Task<ProcessImportQueueResultDto> ProcessImportQueueAsync(ProcessImportQueueRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileImportSummaryDto>> ListFileImportsAsync(CancellationToken cancellationToken = default);
    Task<FileImportDetailsDto?> GetFileImportDetailsAsync(int fileImportId, CancellationToken cancellationToken = default);
    Task<RetryFileImportResultDto> RetryFileImportAsync(RetryFileImportRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceImportResultDto> ImportXmlAsync(InvoiceImportRequest request, CancellationToken cancellationToken = default);
    Task<MatchRunResultDto> RunMatchAsync(int invoiceId, string executedBy, CancellationToken cancellationToken = default);
}
