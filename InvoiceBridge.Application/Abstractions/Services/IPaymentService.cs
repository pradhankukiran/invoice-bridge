using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IPaymentService
{
    Task<IReadOnlyList<PayableInvoiceDto>> ListPayablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentLedgerItemDto>> ListPaymentsAsync(CancellationToken cancellationToken = default);
    Task<int> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);
}
