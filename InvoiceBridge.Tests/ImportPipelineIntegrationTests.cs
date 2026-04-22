using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using InvoiceBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Tests;

public sealed class ImportPipelineIntegrationTests
{
    [Fact]
    public async Task QueueAndProcessImport_ThenRunMatch_PersistsInvoiceAndCreatesApprovalNotification()
    {
        await using var testScope = await IntegrationTestScope.CreateAsync();
        using var scope = testScope.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        const string supplierCode = "SUP-INT-001";
        const string supplierName = "Integration Supplier One";
        const string itemCode = "ITEM-001";

        var poLine = new PurchaseOrderLine
        {
            ItemCode = itemCode,
            Description = "Integration Test Item",
            OrderedQuantity = 10m,
            UnitPrice = 10m,
            TaxRate = 5m
        };

        var purchaseOrder = new PurchaseOrder
        {
            PoNumber = "PO-INT-001",
            Supplier = new Supplier
            {
                SupplierCode = supplierCode,
                LegalName = supplierName,
                Email = "supplier@example.test",
                IsActive = true
            },
            CurrencyCode = "USD",
            Status = PurchaseOrderStatus.Submitted,
            CreatedBy = "procurement.officer",
            Lines = [poLine]
        };

        var goodsReceipt = new GoodsReceipt
        {
            GrnNumber = "GRN-INT-001",
            PurchaseOrder = purchaseOrder,
            ReceivedBy = "warehouse.receiver",
            Lines =
            [
                new GoodsReceiptLine
                {
                    PurchaseOrderLine = poLine,
                    ReceivedQuantity = 10m,
                    DamagedQuantity = 0m
                }
            ]
        };

        dbContext.GoodsReceipts.Add(goodsReceipt);
        await dbContext.SaveChangesAsync();

        var fileImportId = await invoiceService.QueueImportAsync(new InvoiceImportRequest
        {
            FileName = "integration-invoice.xml",
            ImportedBy = "ap.accountant",
            XmlContent = BuildInvoiceXml(
                invoiceNumber: "INV-INT-001",
                supplierCode: supplierCode,
                supplierName: supplierName,
                itemCode: itemCode)
        });

        var processResult = await invoiceService.ProcessImportQueueAsync(new ProcessImportQueueRequest
        {
            BatchSize = 5,
            ProcessedBy = "import.worker"
        });

        Assert.Equal(1, processResult.ProcessedCount);
        Assert.Equal(1, processResult.SucceededCount);
        Assert.Equal(0, processResult.FailedCount);

        var importResult = Assert.Single(processResult.Results);
        Assert.True(importResult.IsSuccess);
        Assert.Equal(fileImportId, importResult.FileImportId);
        Assert.NotNull(importResult.InvoiceId);
        Assert.Equal(FileImportStatus.Completed, importResult.FileImportStatus);

        var importDetails = await invoiceService.GetFileImportDetailsAsync(fileImportId);
        Assert.NotNull(importDetails);
        Assert.Equal(FileImportStatus.Completed, importDetails!.Status);
        Assert.Empty(importDetails.Diagnostics);
        Assert.Single(importDetails.ImportedInvoices);

        var matchResult = await invoiceService.RunMatchAsync(importResult.InvoiceId!.Value, "manager.approver");
        Assert.True(matchResult.IsMatch);
        Assert.Equal(MatchResultCode.Matched, matchResult.ResultCode);

        var invoice = await dbContext.Invoices
            .Include(item => item.ApprovalRequest)
            .SingleAsync(item => item.Id == importResult.InvoiceId.Value);

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.NotNull(invoice.ApprovalRequest);
        Assert.Equal(ApprovalDecision.Pending, invoice.ApprovalRequest!.CurrentDecision);

        var managerNotifications = await notificationService.ListForUserAsync(new NotificationListRequest
        {
            RecipientUsername = "manager.approver",
            MaxRows = 20,
            IncludeRead = true
        });

        Assert.Contains(managerNotifications, notification =>
            notification.Category == "Approval"
            && notification.Title.Contains("INV-INT-001", StringComparison.Ordinal)
            && notification.LinkUrl == "/approvals");

        Assert.Contains(testScope.DigestSender.Messages, message =>
            message.Subject.Contains("INV-INT-001", StringComparison.Ordinal));
    }

    private static string BuildInvoiceXml(string invoiceNumber, string supplierCode, string supplierName, string itemCode)
    {
        return $"""
               <Invoice>
                 <ID>{invoiceNumber}</ID>
                 <IssueDate>2026-02-13</IssueDate>
                 <DocumentCurrencyCode>USD</DocumentCurrencyCode>
                 <AccountingSupplierParty>
                   <Party>
                     <PartyLegalEntity>
                       <CompanyID>{supplierCode}</CompanyID>
                       <RegistrationName>{supplierName}</RegistrationName>
                     </PartyLegalEntity>
                   </Party>
                 </AccountingSupplierParty>
                 <TaxTotal>
                   <TaxAmount>5.00</TaxAmount>
                 </TaxTotal>
                 <LegalMonetaryTotal>
                   <LineExtensionAmount>100.00</LineExtensionAmount>
                   <PayableAmount>105.00</PayableAmount>
                 </LegalMonetaryTotal>
                 <InvoiceLine>
                   <ID>1</ID>
                   <InvoicedQuantity>10</InvoicedQuantity>
                   <LineExtensionAmount>100.00</LineExtensionAmount>
                   <Item>
                     <SellersItemIdentification>
                       <ID>{itemCode}</ID>
                     </SellersItemIdentification>
                     <Description>Integration Test Item</Description>
                     <ClassifiedTaxCategory>
                       <Percent>5</Percent>
                     </ClassifiedTaxCategory>
                   </Item>
                   <Price>
                     <PriceAmount>10.00</PriceAmount>
                   </Price>
                 </InvoiceLine>
               </Invoice>
               """;
    }
}
