using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace InvoiceBridge.Web.Security;

public static class RateLimitingPolicies
{
    public const string AuthLogin = "auth-login";
    public const string ApiDownload = "api-download";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = static (ctx, token) =>
        {
            ctx.HttpContext.Response.Headers["Retry-After"] = "30";
            return ValueTask.CompletedTask;
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ResolveClientKey(context),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.AddPolicy(AuthLogin, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.AddPolicy(ApiDownload, context =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: ResolveClientKey(context),
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    TokensPerPeriod = 10,
                    AutoReplenishment = true
                }));
    }

    private static string ResolveClientKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(context.User.Identity.Name))
        {
            return $"user:{context.User.Identity.Name}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
