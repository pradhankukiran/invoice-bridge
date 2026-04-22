using System.Security.Claims;
using System.Text;
using InvoiceBridge.Application;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Infrastructure;
using InvoiceBridge.Web.Components;
using InvoiceBridge.Web.Security;
using InvoiceBridge.Web.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting InvoiceBridge.Web");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithExceptionDetails();

        if (context.HostingEnvironment.IsDevelopment())
        {
            configuration.WriteTo.Console();
        }
        else
        {
            configuration.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
        }

        configuration.WriteTo.File(
            new Serilog.Formatting.Compact.CompactJsonFormatter(),
            path: Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "invoicebridge-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 64 * 1024 * 1024,
            rollOnFileSizeLimit: true);
    });

    builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddRateLimiter(RateLimitingPolicies.Configure);

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 8 * 1024 * 1024; // 8 MB — invoice XML ceiling
});

builder.Services.Configure<DemoAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<IDemoUserStore, DemoUserStore>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "InvoiceBridge.Auth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AnyAuthenticatedUser, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicies.ProcurementAccess, policy => policy.RequireRole("Admin", "Procurement"));
    options.AddPolicy(AuthorizationPolicies.WarehouseAccess, policy => policy.RequireRole("Admin", "Warehouse"));
    options.AddPolicy(AuthorizationPolicies.ImportAccess, policy => policy.RequireRole("Admin", "AP", "IntegrationAdmin"));
    options.AddPolicy(AuthorizationPolicies.IntegrationAccess, policy => policy.RequireRole("Admin", "IntegrationAdmin"));
    options.AddPolicy(AuthorizationPolicies.InvoiceReviewAccess, policy => policy.RequireRole("Admin", "AP", "Manager"));
    options.AddPolicy(AuthorizationPolicies.ApprovalAccess, policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy(AuthorizationPolicies.ExceptionAccess, policy => policy.RequireRole("Admin", "AP", "Manager"));
    options.AddPolicy(AuthorizationPolicies.ExportAccess, policy => policy.RequireRole("Admin", "Finance"));
    options.AddPolicy(AuthorizationPolicies.PaymentAccess, policy => policy.RequireRole("Admin", "Finance"));
    options.AddPolicy(AuthorizationPolicies.AuditAccess, policy => policy.RequireRole("Admin", "Compliance"));
});

builder.Services.AddApplication();
builder.Services.AddSingleton<IRoleRecipientResolver, DemoRoleRecipientResolver>();

builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var smtpEnabled = builder.Configuration.GetValue<bool>($"{SmtpOptions.SectionName}:Enabled");
if (smtpEnabled)
{
    builder.Services.AddSingleton<INotificationDigestSender, SmtpNotificationDigestSender>();
}
else
{
    builder.Services.AddSingleton<INotificationDigestSender, LoggingNotificationDigestSender>();
}

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<ImportQueueWorker>();
builder.Services.AddHostedService<ApprovalSlaWorker>();
builder.Services.AddHostedService<NotificationOutboxWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = static (ctx, elapsed, ex) =>
        ex != null ? LogEventLevel.Error
        : ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
        : ctx.Response.StatusCode >= 400 ? LogEventLevel.Warning
        : LogEventLevel.Information;
    options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("UserName", httpContext.User.Identity?.Name);
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
    };
});

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseResponseCompression();

await app.Services.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Persistence:SeedOnStartup"))
{
    await app.Services.SeedDatabaseAsync();
}

app.MapPost("/auth/login", async (HttpContext context, IDemoUserStore userStore) =>
{
    var form = await context.Request.ReadFormAsync();

    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

    var user = userStore.ValidateCredentials(username, password);
    if (user is null)
    {
        var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
        return Results.Redirect($"/login?error=1&returnUrl={encodedReturnUrl}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Username),
        new(ClaimTypes.Name, user.Username)
    };

    claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var principal = new ClaimsPrincipal(
        new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        });

    return Results.Redirect(returnUrl);
}).RequireRateLimiting(RateLimitingPolicies.AuthLogin);

app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapGet("/api/exports/{exportId:int}/download", async (
    int exportId,
    IAccountingExportService accountingExportService,
    CancellationToken cancellationToken) =>
{
    var artifact = await accountingExportService.GetDownloadAsync(exportId, cancellationToken);
    if (artifact is null)
    {
        return Results.NotFound();
    }

    var bytes = Encoding.UTF8.GetBytes(artifact.Content);
    return Results.File(bytes, artifact.ContentType, artifact.FileName);
}).RequireAuthorization(AuthorizationPolicies.ExportAccess)
  .RequireRateLimiting(RateLimitingPolicies.ApiDownload);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "InvoiceBridge.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string NormalizeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        return "/";
    }

    return returnUrl;
}
