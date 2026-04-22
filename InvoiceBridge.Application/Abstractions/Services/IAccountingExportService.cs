using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IAccountingExportService
{
    Task<IReadOnlyList<ExportCandidateInvoiceDto>> ListEligibleInvoicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountingExportSummaryDto>> ListExportsAsync(CancellationToken cancellationToken = default);
    Task<AccountingExportResultDto> CreateExportAsync(CreateAccountingExportRequest request, CancellationToken cancellationToken = default);
    Task<string?> GetPayloadAsync(int exportId, CancellationToken cancellationToken = default);
    Task<AccountingExportDownloadDto?> GetDownloadAsync(int exportId, CancellationToken cancellationToken = default);
}
