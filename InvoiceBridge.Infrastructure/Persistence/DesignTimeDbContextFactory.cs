using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceBridge.Infrastructure.Persistence;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InvoiceBridgeDbContext>
{
    public InvoiceBridgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("INVOICEBRIDGE_DESIGNTIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=invoicebridge_design;Username=invoicebridge;Password=invoicebridge";

        var builder = new DbContextOptionsBuilder<InvoiceBridgeDbContext>();
        builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(InvoiceBridgeDbContext).Assembly.FullName));

        return new InvoiceBridgeDbContext(builder.Options);
    }
}
