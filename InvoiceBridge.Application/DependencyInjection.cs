using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IExceptionService, ExceptionService>();
        services.AddScoped<IAccountingExportService, AccountingExportService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IMappingProfileService, MappingProfileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationPublisher>(provider =>
            provider.GetRequiredService<INotificationService>() as INotificationPublisher
            ?? throw new InvalidOperationException("Notification service does not implement notification publisher."));
        services.AddSingleton<IRoleRecipientResolver, NullRoleRecipientResolver>();
        services.AddSingleton<INotificationDigestSender, NullNotificationDigestSender>();

        return services;
    }
}
