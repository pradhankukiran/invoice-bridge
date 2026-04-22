namespace InvoiceBridge.Application.Abstractions.Services;

public interface IAuditContextAccessor
{
    AuditContext Current { get; }
}

public sealed record AuditContext(string? IpAddress, string? UserAgent, string? CorrelationId)
{
    public static AuditContext Empty { get; } = new(null, null, null);
}

public sealed class NullAuditContextAccessor : IAuditContextAccessor
{
    public AuditContext Current => AuditContext.Empty;
}
