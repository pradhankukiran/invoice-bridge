using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Infrastructure.Persistence;
using InvoiceBridge.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;

namespace InvoiceBridge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlServerConnectionString = configuration.GetConnectionString("SqlServer");
        var sqliteConnectionString = configuration.GetConnectionString("Sqlite") ?? "Data Source=invoicebridge.db";

        services.AddDbContext<InvoiceBridgeDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
            {
                options.UseSqlServer(sqlServerConnectionString);
            }
            else
            {
                options.UseSqlite(sqliteConnectionString);
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<InvoiceBridgeDbContext>());

        return services;
    }

    public static async Task InitialiseDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        if (await IsLegacySqliteSchemaAsync(dbContext))
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }

        try
        {
            await SeedData.EnsureSeededAsync(dbContext);
        }
        catch (Exception exception) when (IsSchemaMismatch(exception))
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await SeedData.EnsureSeededAsync(dbContext);
        }
    }

    private static bool IsSchemaMismatch(Exception exception)
    {
        if (exception is SqliteException sqliteException
            && (sqliteException.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                || sqliteException.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (exception.InnerException is not null)
        {
            return IsSchemaMismatch(exception.InnerException);
        }

        return false;
    }

    private static async Task<bool> IsLegacySqliteSchemaAsync(InvoiceBridgeDbContext dbContext)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return false;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var hasSupplierMappingProfiles = await TableExistsAsync(connection, "SupplierMappingProfiles");
            if (!hasSupplierMappingProfiles)
            {
                return true;
            }

            var hasExpectedFileImportColumns = await HasColumnsAsync(
                connection,
                tableName: "FileImports",
                requiredColumns:
                [
                    "XmlContent",
                    "XsdContent",
                    "ProcessingStartedAtUtc",
                    "ProcessedAtUtc",
                    "NextRetryAtUtc",
                    "RetryCount",
                    "LastErrorMessage"
                ]);

            if (!hasExpectedFileImportColumns)
            {
                return true;
            }

            var hasUserNotifications = await TableExistsAsync(connection, "UserNotifications");
            if (!hasUserNotifications)
            {
                return true;
            }

            var hasApprovalNotificationColumns = await HasColumnsAsync(
                connection,
                tableName: "ApprovalRequests",
                requiredColumns:
                [
                    "EscalationNotifiedAtUtc",
                    "BreachNotifiedAtUtc"
                ]);

            if (!hasApprovalNotificationColumns)
            {
                return true;
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";

        var tableNameParameter = command.CreateParameter();
        tableNameParameter.ParameterName = "$name";
        tableNameParameter.Value = tableName;
        command.Parameters.Add(tableNameParameter);

        var result = await command.ExecuteScalarAsync();
        var count = result switch
        {
            long value => value,
            int value => value,
            _ => 0
        };

        return count > 0;
    }

    private static async Task<bool> HasColumnsAsync(DbConnection connection, string tableName, IEnumerable<string> requiredColumns)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"] is string columnName)
            {
                existingColumns.Add(columnName);
            }
        }

        return requiredColumns.All(existingColumns.Contains);
    }
}
