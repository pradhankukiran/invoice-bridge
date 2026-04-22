using InvoiceBridge.Application;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Infrastructure.Persistence;
using InvoiceBridge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Tests;

internal sealed class IntegrationTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _anchorConnection;
    private readonly ServiceProvider _serviceProvider;

    private IntegrationTestScope(
        SqliteConnection anchorConnection,
        ServiceProvider serviceProvider,
        CapturingNotificationDigestSender digestSender)
    {
        _anchorConnection = anchorConnection;
        _serviceProvider = serviceProvider;
        DigestSender = digestSender;
    }

    public CapturingNotificationDigestSender DigestSender { get; }

    public IServiceScope CreateScope()
    {
        return _serviceProvider.CreateScope();
    }

    public static async Task<IntegrationTestScope> CreateAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source=file:invoicebridge-tests-{databaseName}?mode=memory&cache=shared";

        var anchorConnection = new SqliteConnection(connectionString);
        await anchorConnection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<RowVersionInterceptor>();
        services.AddDbContext<InvoiceBridgeDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<RowVersionInterceptor>());
            options.UseSqlite(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<InvoiceBridgeDbContext>());
        services.AddSingleton<IRoleRecipientResolver, TestRoleRecipientResolver>();

        var digestSender = new CapturingNotificationDigestSender();
        services.AddSingleton<INotificationDigestSender>(digestSender);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        return new IntegrationTestScope(anchorConnection, provider, digestSender);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _anchorConnection.DisposeAsync();
    }
}

internal sealed class TestRoleRecipientResolver : IRoleRecipientResolver
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RoleUserMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = ["integration.admin"],
            ["Procurement"] = ["procurement.officer"],
            ["Warehouse"] = ["warehouse.receiver"],
            ["AP"] = ["ap.accountant"],
            ["Manager"] = ["manager.approver"],
            ["Finance"] = ["finance.officer"],
            ["Compliance"] = ["compliance.auditor"],
            ["IntegrationAdmin"] = ["integration.admin"]
        };

    public Task<IReadOnlyList<string>> ResolveUsersByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return Task.FromResult(
            RoleUserMap.TryGetValue(role.Trim(), out var users)
                ? users
                : []);
    }
}

internal sealed class CapturingNotificationDigestSender : INotificationDigestSender
{
    private readonly List<NotificationDigestMessage> _messages = [];

    public IReadOnlyList<NotificationDigestMessage> Messages => _messages;

    public Task SendAsync(NotificationDigestMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
