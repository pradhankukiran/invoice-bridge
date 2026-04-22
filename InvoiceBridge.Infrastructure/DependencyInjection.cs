using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Infrastructure.Persistence;
using InvoiceBridge.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Infrastructure;

public enum PersistenceProvider
{
    Sqlite,
    SqlServer,
    Postgres
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = ResolveProvider(configuration);
        var connectionString = ResolveConnectionString(configuration, provider);

        services.AddSingleton(new PersistenceSettings(provider, connectionString));

        services.AddDbContext<InvoiceBridgeDbContext>(options =>
        {
            switch (provider)
            {
                case PersistenceProvider.Postgres:
                    options.UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(InvoiceBridgeDbContext).Assembly.FullName);
                        npgsql.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null);
                    });
                    break;

                case PersistenceProvider.SqlServer:
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.MigrationsAssembly(typeof(InvoiceBridgeDbContext).Assembly.FullName);
                        sql.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
                    break;

                case PersistenceProvider.Sqlite:
                default:
                    options.UseSqlite(connectionString);
                    break;
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<InvoiceBridgeDbContext>());

        return services;
    }

    public static async Task InitialiseDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<PersistenceSettings>();

        if (settings.Provider == PersistenceProvider.Sqlite)
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }

    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        await SeedData.EnsureSeededAsync(dbContext, cancellationToken);
    }

    private static PersistenceProvider ResolveProvider(IConfiguration configuration)
    {
        var explicitProvider = configuration["Persistence:Provider"];
        if (!string.IsNullOrWhiteSpace(explicitProvider) &&
            Enum.TryParse<PersistenceProvider>(explicitProvider, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")))
        {
            return PersistenceProvider.Postgres;
        }

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer")))
        {
            return PersistenceProvider.SqlServer;
        }

        return PersistenceProvider.Sqlite;
    }

    private static string ResolveConnectionString(IConfiguration configuration, PersistenceProvider provider)
    {
        return provider switch
        {
            PersistenceProvider.Postgres => configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "Persistence provider is Postgres but ConnectionStrings:Postgres is not configured."),
            PersistenceProvider.SqlServer => configuration.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException(
                    "Persistence provider is SqlServer but ConnectionStrings:SqlServer is not configured."),
            PersistenceProvider.Sqlite => configuration.GetConnectionString("Sqlite")
                ?? "Data Source=invoicebridge.db",
            _ => throw new InvalidOperationException($"Unsupported persistence provider '{provider}'.")
        };
    }
}

public sealed record PersistenceSettings(PersistenceProvider Provider, string ConnectionString);
