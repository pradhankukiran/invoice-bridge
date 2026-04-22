using System.Security.Claims;
using System.Text;
using InvoiceBridge.Application;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Infrastructure;
using InvoiceBridge.Web.Components;
using InvoiceBridge.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

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
builder.Services.AddSingleton<INotificationDigestSender, LoggingNotificationDigestSender>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseResponseCompression();

await app.Services.InitialiseDatabaseAsync();

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
});

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
}).RequireAuthorization(AuthorizationPolicies.ExportAccess);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();

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
