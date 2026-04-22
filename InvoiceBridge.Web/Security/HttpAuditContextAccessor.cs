using InvoiceBridge.Application.Abstractions.Services;

namespace InvoiceBridge.Web.Security;

public sealed class HttpAuditContextAccessor(IHttpContextAccessor httpContextAccessor) : IAuditContextAccessor
{
    public AuditContext Current
    {
        get
        {
            var http = httpContextAccessor.HttpContext;
            if (http is null)
            {
                return AuditContext.Empty;
            }

            var ip = http.Connection.RemoteIpAddress?.ToString();
            var userAgent = http.Request.Headers.UserAgent.ToString();
            var correlationId = http.TraceIdentifier;

            return new AuditContext(
                IpAddress: string.IsNullOrWhiteSpace(ip) ? null : ip,
                UserAgent: string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                CorrelationId: string.IsNullOrWhiteSpace(correlationId) ? null : correlationId);
        }
    }
}

