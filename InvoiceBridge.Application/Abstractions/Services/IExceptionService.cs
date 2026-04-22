using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IExceptionService
{
    Task<IReadOnlyList<ExceptionInvoiceDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ExceptionResolutionResultDto> ResolveAsync(ExceptionResolutionRequest request, CancellationToken cancellationToken = default);
}
