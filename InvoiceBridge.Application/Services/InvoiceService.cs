using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FluentValidation;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Application.Matching;
using InvoiceBridge.Application.Workflow;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class InvoiceService(
    IApplicationDbContext dbContext,
    INotificationPublisher notificationPublisher,
    IValidator<InvoiceImportRequest> importRequestValidator) : IInvoiceService
{
    private const int MaxQueueBatchSize = 100;

    public async Task<IReadOnlyList<InvoiceSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.MatchResults)
            .ToListAsync(cancellationToken);

        return invoices
            .Select(i => new InvoiceSummaryDto(
                i.Id,
                i.InvoiceNumber,
                i.Supplier.LegalName,
                i.Status,
                i.InvoiceDate,
                i.TotalAmount,
                i.CreatedAtUtc,
                i.MatchResults
                    .OrderByDescending(m => m.ExecutedAtUtc)
                    .Select(m => (DateTimeOffset?)m.ExecutedAtUtc)
                    .FirstOrDefault()))
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToList();
    }

    public async Task<int> QueueImportAsync(InvoiceImportRequest request, CancellationToken cancellationToken = default)
    {
        await importRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var actor = string.IsNullOrWhiteSpace(request.ImportedBy) ? "ap.user" : request.ImportedBy.Trim();
        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? $"invoice-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"
            : request.FileName.Trim();

        var fileImport = new FileImport
        {
            FileName = fileName,
            ImportedBy = actor,
            XmlContent = request.XmlContent,
            XsdContent = request.XsdContent,
            Status = FileImportStatus.Queued,
            ImportedAtUtc = DateTimeOffset.UtcNow,
            NextRetryAtUtc = null,
            RetryCount = 0,
            ErrorCount = 0
        };

        dbContext.FileImports.Add(fileImport);

        AuditTrailWriter.Add(
            dbContext,
            entityName: "FileImport",
            entityId: fileName,
            action: "InvoiceImportQueued",
            actor: actor,
            details: "Status=Queued.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return fileImport.Id;
    }

    public async Task<ProcessImportQueueResultDto> ProcessImportQueueAsync(ProcessImportQueueRequest request, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(request.BatchSize <= 0 ? 10 : request.BatchSize, 1, MaxQueueBatchSize);
        var actor = string.IsNullOrWhiteSpace(request.ProcessedBy) ? "import.worker" : request.ProcessedBy.Trim();
        var now = DateTimeOffset.UtcNow;
        var eligibleStatuses = new HashSet<FileImportStatus>
        {
            FileImportStatus.Queued,
            FileImportStatus.Pending,
            FileImportStatus.QueuedForRetry,
            FileImportStatus.Validated
        };

        var queueCandidates = await dbContext.FileImports
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.Status,
                item.NextRetryAtUtc,
                item.ImportedAtUtc
            })
            .ToListAsync(cancellationToken);

        var candidateIds = queueCandidates
            .Where(item =>
                eligibleStatuses.Contains(item.Status)
                && (item.NextRetryAtUtc == null || item.NextRetryAtUtc <= now))
            .OrderBy(item => item.NextRetryAtUtc ?? item.ImportedAtUtc)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .Select(item => item.Id)
            .ToList();

        var results = new List<InvoiceImportResultDto>(candidateIds.Count);

        foreach (var fileImportId in candidateIds)
        {
            var result = await ProcessQueuedImportAsync(fileImportId, actor, cancellationToken);
            results.Add(result);
        }

        return new ProcessImportQueueResultDto
        {
            RequestedBatchSize = batchSize,
            ProcessedCount = results.Count,
            SucceededCount = results.Count(item => item.IsSuccess),
            FailedCount = results.Count(item => !item.IsSuccess),
            Results = results
        };
    }

    public async Task<IReadOnlyList<FileImportSummaryDto>> ListFileImportsAsync(CancellationToken cancellationToken = default)
    {
        var imports = await dbContext.FileImports
            .AsNoTracking()
            .Select(item => new FileImportSummaryDto(
                item.Id,
                item.FileName,
                item.ImportedBy,
                item.ImportedAtUtc,
                item.ProcessingStartedAtUtc,
                item.ProcessedAtUtc,
                item.NextRetryAtUtc,
                item.Status,
                item.RetryCount,
                item.ErrorCount,
                item.Invoices.Count,
                item.LastErrorMessage))
            .ToListAsync(cancellationToken);

        return imports
            .OrderByDescending(item => item.ImportedAtUtc)
            .ThenByDescending(item => item.FileImportId)
            .Take(500)
            .ToList();
    }

    public async Task<FileImportDetailsDto?> GetFileImportDetailsAsync(int fileImportId, CancellationToken cancellationToken = default)
    {
        var fileImport = await dbContext.FileImports
            .AsNoTracking()
            .Include(item => item.Errors)
            .Include(item => item.Invoices)
            .ThenInclude(item => item.Supplier)
            .SingleOrDefaultAsync(item => item.Id == fileImportId, cancellationToken);

        if (fileImport is null)
        {
            return null;
        }

        return new FileImportDetailsDto
        {
            FileImportId = fileImport.Id,
            FileName = fileImport.FileName,
            ImportedBy = fileImport.ImportedBy,
            ImportedAtUtc = fileImport.ImportedAtUtc,
            ProcessingStartedAtUtc = fileImport.ProcessingStartedAtUtc,
            ProcessedAtUtc = fileImport.ProcessedAtUtc,
            NextRetryAtUtc = fileImport.NextRetryAtUtc,
            Status = fileImport.Status,
            RetryCount = fileImport.RetryCount,
            ErrorCount = fileImport.ErrorCount,
            LastErrorMessage = fileImport.LastErrorMessage,
            Diagnostics = fileImport.Errors
                .OrderBy(item => item.Id)
                .Select(item => new FileImportDiagnosticsDto(
                    item.Id,
                    item.Path,
                    item.Message,
                    item.Severity))
                .ToList(),
            ImportedInvoices = fileImport.Invoices
                .OrderBy(item => item.Id)
                .Select(item => new FileImportInvoiceDto(
                    item.Id,
                    item.InvoiceNumber,
                    item.Supplier.LegalName,
                    item.Status,
                    item.TotalAmount))
                .ToList()
        };
    }

    public async Task<RetryFileImportResultDto> RetryFileImportAsync(RetryFileImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FileImportId <= 0)
        {
            throw new ArgumentException("File import id is required.", nameof(request.FileImportId));
        }

        var fileImport = await dbContext.FileImports
            .SingleOrDefaultAsync(item => item.Id == request.FileImportId, cancellationToken)
            ?? throw new InvalidOperationException("File import not found.");

        if (fileImport.Status != FileImportStatus.Failed)
        {
            return new RetryFileImportResultDto
            {
                FileImportId = fileImport.Id,
                Status = fileImport.Status,
                Message = "Only failed imports can be queued for retry."
            };
        }

        if (string.IsNullOrWhiteSpace(fileImport.XmlContent))
        {
            throw new InvalidOperationException("Import payload is unavailable for retry.");
        }

        var actor = string.IsNullOrWhiteSpace(request.RequestedBy) ? "ap.accountant" : request.RequestedBy.Trim();
        var delaySeconds = Math.Max(0, request.DelaySeconds);

        fileImport.Status = FileImportStatus.QueuedForRetry;
        fileImport.NextRetryAtUtc = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        fileImport.LastErrorMessage = null;
        fileImport.RetryCount += 1;

        AuditTrailWriter.Add(
            dbContext,
            entityName: "FileImport",
            entityId: fileImport.Id.ToString(CultureInfo.InvariantCulture),
            action: "InvoiceImportRetryQueued",
            actor: actor,
            details: $"RetryCount={fileImport.RetryCount}; NextRetryAt={fileImport.NextRetryAtUtc:O}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RetryFileImportResultDto
        {
            FileImportId = fileImport.Id,
            Status = fileImport.Status,
            Message = "Import queued for retry."
        };
    }

    public async Task<InvoiceImportResultDto> ImportXmlAsync(InvoiceImportRequest request, CancellationToken cancellationToken = default)
    {
        var fileImportId = await QueueImportAsync(request, cancellationToken);
        return await ProcessQueuedImportAsync(fileImportId, request.ImportedBy, cancellationToken);
    }

    public async Task<MatchRunResultDto> RunMatchAsync(int invoiceId, string executedBy, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Supplier)
            .Include(i => i.ApprovalRequest)
            .SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new InvalidOperationException("Invoice not found.");
        }

        var poLines = await dbContext.PurchaseOrderLines
            .Include(l => l.PurchaseOrder)
            .Include(l => l.GoodsReceiptLines)
            .Where(l => l.PurchaseOrder.SupplierId == invoice.SupplierId)
            .ToListAsync(cancellationToken);

        var poLookup = poLines
            .GroupBy(line => line.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(line => line.PurchaseOrder.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var resultLines = new List<MatchResultLine>();
        var lineResults = new List<MatchLineResultDto>();

        foreach (var line in invoice.Lines)
        {
            if (!poLookup.TryGetValue(line.ItemCode, out var poLine))
            {
                var noPoResult = new MatchResultLine
                {
                    InvoiceLineId = line.Id,
                    PurchaseOrderLineId = null,
                    QuantityVariance = line.BilledQuantity,
                    PriceVariance = line.UnitPrice,
                    TaxVariance = line.TaxRate,
                    ResultCode = MatchResultCode.MissingPurchaseOrderLine
                };

                resultLines.Add(noPoResult);
                lineResults.Add(new MatchLineResultDto(line.Id, line.ItemCode, noPoResult.ResultCode, noPoResult.QuantityVariance, noPoResult.PriceVariance, noPoResult.TaxVariance));
                continue;
            }

            var receivedQuantity = poLine.GoodsReceiptLines.Sum(grnLine => grnLine.ReceivedQuantity);
            var hasGoodsReceipt = receivedQuantity > 0;
            var baselineQuantity = hasGoodsReceipt ? receivedQuantity : poLine.OrderedQuantity;

            var code = MatchRuleEvaluator.Evaluate(
                line.BilledQuantity,
                baselineQuantity,
                line.UnitPrice,
                poLine.UnitPrice,
                line.TaxRate,
                poLine.TaxRate,
                hasGoodsReceipt);

            var quantityVariance = line.BilledQuantity - baselineQuantity;
            var priceVariance = line.UnitPrice - poLine.UnitPrice;
            var taxVariance = line.TaxRate - poLine.TaxRate;

            resultLines.Add(new MatchResultLine
            {
                InvoiceLineId = line.Id,
                PurchaseOrderLineId = poLine.Id,
                QuantityVariance = quantityVariance,
                PriceVariance = priceVariance,
                TaxVariance = taxVariance,
                ResultCode = code
            });

            lineResults.Add(new MatchLineResultDto(line.Id, line.ItemCode, code, quantityVariance, priceVariance, taxVariance));
        }

        var isMatch = resultLines.All(line => line.ResultCode == MatchResultCode.Matched);
        var overallCode = isMatch ? MatchResultCode.Matched : resultLines.First(line => line.ResultCode != MatchResultCode.Matched).ResultCode;

        var matchResult = new MatchResult
        {
            InvoiceId = invoice.Id,
            ExecutedBy = string.IsNullOrWhiteSpace(executedBy) ? "system" : executedBy.Trim(),
            IsMatch = isMatch,
            ResultCode = overallCode,
            Notes = isMatch ? "Invoice matched successfully." : "Invoice has variances that require review.",
            Lines = resultLines
        };

        dbContext.MatchResults.Add(matchResult);
        var approvalRequestCreated = false;
        var approvalAssignedRole = string.Empty;

        if (isMatch)
        {
            invoice.Status = InvoiceStatus.PendingApproval;

            if (invoice.ApprovalRequest is null)
            {
                approvalAssignedRole = "Manager";
                approvalRequestCreated = true;

                dbContext.ApprovalRequests.Add(new ApprovalRequest
                {
                    InvoiceId = invoice.Id,
                    AssignedRole = approvalAssignedRole,
                    CurrentDecision = ApprovalDecision.Pending
                });
            }
        }
        else
        {
            invoice.Status = InvoiceStatus.Exception;
        }

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Invoice",
            entityId: invoice.Id.ToString(CultureInfo.InvariantCulture),
            action: "InvoiceMatchExecuted",
            actor: executedBy,
            details: $"Result={matchResult.ResultCode}; IsMatch={matchResult.IsMatch}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        if (approvalRequestCreated)
        {
            await notificationPublisher.PublishToRoleAsync(
                approvalAssignedRole,
                new NotificationPublishRequest
                {
                    Category = "Approval",
                    Severity = "Info",
                    Title = $"Approval required for invoice {invoice.InvoiceNumber}",
                    Message = $"Invoice {invoice.InvoiceNumber} for {invoice.Supplier.LegalName} is pending approval. Total {invoice.TotalAmount:0.00} {invoice.CurrencyCode}.",
                    LinkUrl = "/approvals",
                    SourceEntityName = "Invoice",
                    SourceEntityId = invoice.Id.ToString(CultureInfo.InvariantCulture),
                    Actor = string.IsNullOrWhiteSpace(executedBy) ? "system" : executedBy.Trim(),
                    SendDigest = true
                },
                cancellationToken);
        }

        return new MatchRunResultDto
        {
            MatchResultId = matchResult.Id,
            IsMatch = matchResult.IsMatch,
            ResultCode = matchResult.ResultCode,
            Lines = lineResults
        };
    }

    private async Task<InvoiceImportResultDto> ProcessQueuedImportAsync(int fileImportId, string actor, CancellationToken cancellationToken)
    {
        var fileImport = await dbContext.FileImports
            .Include(item => item.Errors)
            .SingleOrDefaultAsync(item => item.Id == fileImportId, cancellationToken)
            ?? throw new InvalidOperationException("File import not found.");

        if (fileImport.Status == FileImportStatus.Completed)
        {
            return new InvoiceImportResultDto
            {
                FileImportId = fileImport.Id,
                IsSuccess = true,
                Message = "File import is already completed.",
                FileImportStatus = fileImport.Status
            };
        }

        if (fileImport.Status == FileImportStatus.Processing)
        {
            return new InvoiceImportResultDto
            {
                FileImportId = fileImport.Id,
                IsSuccess = false,
                Message = "File import is currently processing.",
                FileImportStatus = fileImport.Status,
                Errors = ["File import is already in processing state."]
            };
        }

        var processedBy = string.IsNullOrWhiteSpace(actor) ? fileImport.ImportedBy : actor.Trim();

        if (string.IsNullOrWhiteSpace(fileImport.XmlContent))
        {
            return await FailImportAsync(
                fileImport,
                [new ImportDiagnostic("Invoice", "Invoice XML payload is empty.", "Error")],
                processedBy,
                action: "InvoiceImportPayloadMissing",
                message: "Import payload is empty.",
                cancellationToken);
        }

        if (fileImport.Errors.Count > 0)
        {
            dbContext.FileImportErrors.RemoveRange(fileImport.Errors);
            fileImport.Errors.Clear();
        }

        fileImport.Status = FileImportStatus.Processing;
        fileImport.ProcessingStartedAtUtc = DateTimeOffset.UtcNow;
        fileImport.ProcessedAtUtc = null;
        fileImport.ErrorCount = 0;
        fileImport.LastErrorMessage = null;
        fileImport.NextRetryAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var validationDiagnostics = ValidateXml(fileImport.XmlContent, fileImport.XsdContent);
        if (validationDiagnostics.Count > 0)
        {
            return await FailImportAsync(
                fileImport,
                validationDiagnostics,
                processedBy,
                action: "InvoiceImportValidationFailed",
                message: "XML validation failed.",
                cancellationToken);
        }

        ParsedInvoice parsedInvoice;
        try
        {
            parsedInvoice = ParseInvoiceXml(fileImport.XmlContent);
        }
        catch (Exception ex)
        {
            var path = ex is XmlException xmlException && xmlException.LineNumber > 0
                ? $"XML(Line {xmlException.LineNumber}, Position {xmlException.LinePosition})"
                : "Invoice";

            return await FailImportAsync(
                fileImport,
                [new ImportDiagnostic(path, ex.Message, "Error")],
                processedBy,
                action: "InvoiceImportParsingFailed",
                message: "Unable to parse invoice XML.",
                cancellationToken);
        }

        var supplier = await ResolveSupplierAsync(parsedInvoice, cancellationToken);

        var mappingResult = await ApplySupplierMappingAsync(parsedInvoice, supplier, cancellationToken);
        parsedInvoice = mappingResult.ParsedInvoice;

        if (mappingResult.ProfileId.HasValue)
        {
            AuditTrailWriter.Add(
                dbContext,
                entityName: "SupplierMappingProfile",
                entityId: mappingResult.ProfileId.Value.ToString(CultureInfo.InvariantCulture),
                action: "ProfileAppliedDuringImport",
                actor: processedBy,
                details: $"MappedLines={mappingResult.MappedLineCount}; UnmappedLines={mappingResult.UnmappedLineCount}; Supplier={supplier.SupplierCode}.");
        }

        if (mappingResult.Diagnostics.Count > 0)
        {
            return await FailImportAsync(
                fileImport,
                mappingResult.Diagnostics,
                processedBy,
                action: "InvoiceImportMappingFailed",
                message: "Supplier mapping validation failed.",
                cancellationToken);
        }

        var duplicateInvoice = await dbContext.Invoices.AnyAsync(
            i => i.SupplierId == supplier.Id && i.InvoiceNumber == parsedInvoice.InvoiceNumber,
            cancellationToken);

        if (duplicateInvoice)
        {
            return await FailImportAsync(
                fileImport,
                [new ImportDiagnostic("Invoice/ID", "Duplicate invoice number for supplier.", "Error")],
                processedBy,
                action: "InvoiceImportDuplicateRejected",
                message: "Duplicate invoice detected.",
                cancellationToken);
        }

        var invoice = new Invoice
        {
            InvoiceNumber = parsedInvoice.InvoiceNumber,
            SupplierId = supplier.Id,
            FileImportId = fileImport.Id,
            CurrencyCode = parsedInvoice.CurrencyCode,
            InvoiceDate = parsedInvoice.InvoiceDate,
            DueDate = parsedInvoice.DueDate,
            Subtotal = parsedInvoice.Subtotal,
            TaxAmount = parsedInvoice.TaxAmount,
            TotalAmount = parsedInvoice.TotalAmount,
            Status = InvoiceStatus.Imported,
            Lines = parsedInvoice.Lines.Select(line => new InvoiceLine
            {
                LineNumber = line.LineNumber,
                ItemCode = line.ItemCode,
                Description = line.Description,
                BilledQuantity = line.BilledQuantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                LineTotal = line.LineTotal
            }).ToList()
        };

        dbContext.Invoices.Add(invoice);

        fileImport.Status = FileImportStatus.Completed;
        fileImport.ProcessedAtUtc = DateTimeOffset.UtcNow;
        fileImport.ErrorCount = 0;
        fileImport.LastErrorMessage = null;
        fileImport.NextRetryAtUtc = null;

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Invoice",
            entityId: invoice.InvoiceNumber,
            action: "InvoiceImported",
            actor: processedBy,
            details: $"SupplierId={supplier.Id}; Total={invoice.TotalAmount:0.00}; FileImportId={fileImport.Id}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InvoiceImportResultDto
        {
            FileImportId = fileImport.Id,
            InvoiceId = invoice.Id,
            IsSuccess = true,
            Message = "Invoice imported successfully.",
            FileImportStatus = fileImport.Status
        };
    }

    private async Task<InvoiceImportResultDto> FailImportAsync(
        FileImport fileImport,
        IReadOnlyList<ImportDiagnostic> diagnostics,
        string actor,
        string action,
        string message,
        CancellationToken cancellationToken)
    {
        fileImport.Status = FileImportStatus.Failed;
        fileImport.ProcessedAtUtc = DateTimeOffset.UtcNow;
        fileImport.ErrorCount = diagnostics.Count;
        fileImport.LastErrorMessage = diagnostics.FirstOrDefault()?.Message;

        fileImport.Errors = diagnostics
            .Select(item => new FileImportError
            {
                Path = Truncate(item.Path, 256),
                Message = Truncate(item.Message, 1000),
                Severity = Truncate(item.Severity, 16)
            })
            .ToList();

        AuditTrailWriter.Add(
            dbContext,
            entityName: "FileImport",
            entityId: fileImport.Id.ToString(CultureInfo.InvariantCulture),
            action: action,
            actor: actor,
            details: $"ErrorCount={diagnostics.Count}; LastError={Truncate(fileImport.LastErrorMessage ?? "n/a", 200)}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InvoiceImportResultDto
        {
            FileImportId = fileImport.Id,
            IsSuccess = false,
            Message = message,
            FileImportStatus = fileImport.Status,
            Errors = diagnostics.Select(item => item.Message).ToList()
        };
    }

    private async Task<Supplier> ResolveSupplierAsync(ParsedInvoice parsedInvoice, CancellationToken cancellationToken)
    {
        var supplierCode = string.IsNullOrWhiteSpace(parsedInvoice.SupplierCode)
            ? parsedInvoice.SupplierName.ToUpperInvariant().Replace(' ', '-')
            : parsedInvoice.SupplierCode.Trim().ToUpperInvariant();

        var existingSupplier = await dbContext.Suppliers.FirstOrDefaultAsync(
            s => s.SupplierCode == supplierCode,
            cancellationToken);

        if (existingSupplier is not null)
        {
            return existingSupplier;
        }

        var supplier = new Supplier
        {
            SupplierCode = supplierCode,
            LegalName = parsedInvoice.SupplierName,
            Email = null,
            IsActive = true
        };

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);

        return supplier;
    }

    private static List<ImportDiagnostic> ValidateXml(string xmlContent, string? xsdContent)
    {
        var diagnostics = new List<ImportDiagnostic>();

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = string.IsNullOrWhiteSpace(xsdContent) ? ValidationType.None : ValidationType.Schema
        };

        if (!string.IsNullOrWhiteSpace(xsdContent))
        {
            using var xsdReader = XmlReader.Create(new StringReader(xsdContent));
            settings.Schemas = new XmlSchemaSet();
            settings.Schemas.Add(null, xsdReader);
            settings.ValidationEventHandler += (_, args) =>
            {
                var lineInfo = args.Exception is not null && args.Exception.LineNumber > 0
                    ? $"XML(Line {args.Exception.LineNumber}, Position {args.Exception.LinePosition})"
                    : "XML/Schema";

                diagnostics.Add(new ImportDiagnostic(
                    lineInfo,
                    args.Message,
                    args.Severity == XmlSeverityType.Warning ? "Warning" : "Error"));
            };
        }

        try
        {
            using var xmlReader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (xmlReader.Read())
            {
            }
        }
        catch (Exception ex) when (ex is XmlException or XmlSchemaValidationException)
        {
            var path = ex is XmlException xmlException && xmlException.LineNumber > 0
                ? $"XML(Line {xmlException.LineNumber}, Position {xmlException.LinePosition})"
                : "XML";
            diagnostics.Add(new ImportDiagnostic(path, ex.Message, "Error"));
        }

        return diagnostics;
    }

    private static ParsedInvoice ParseInvoiceXml(string xmlContent)
    {
        var document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("XML root element is missing.");

        var invoiceNumber = GetFirstChildValue(root, "ID")
            ?? throw new InvalidOperationException("Invoice number (ID) is missing.");

        var supplierCode =
            GetPathValue(root, "AccountingSupplierParty", "Party", "PartyLegalEntity", "CompanyID")
            ?? GetPathValue(root, "AccountingSupplierParty", "Party", "EndpointID");

        var supplierName =
            GetPathValue(root, "AccountingSupplierParty", "Party", "PartyLegalEntity", "RegistrationName")
            ?? GetPathValue(root, "AccountingSupplierParty", "Party", "PartyName", "Name")
            ?? "Unknown Supplier";

        var invoiceDateRaw = GetFirstChildValue(root, "IssueDate") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dueDateRaw = GetFirstChildValue(root, "DueDate");

        var currencyCode =
            GetFirstChildValue(root, "DocumentCurrencyCode")
            ?? GetAttributeValue(root.Descendants().FirstOrDefault(e => e.Name.LocalName == "LineExtensionAmount"), "currencyID")
            ?? "USD";

        var subtotal = ParseDecimal(GetPathValue(root, "LegalMonetaryTotal", "LineExtensionAmount"));
        var taxAmount = ParseDecimal(GetPathValue(root, "TaxTotal", "TaxAmount"));
        var totalAmount = ParseDecimal(GetPathValue(root, "LegalMonetaryTotal", "PayableAmount"));

        var invoiceLines = root.Descendants()
            .Where(e => e.Name.LocalName == "InvoiceLine")
            .Select((lineElement, index) => ParseInvoiceLine(lineElement, index + 1))
            .ToList();

        if (invoiceLines.Count == 0)
        {
            throw new InvalidOperationException("No invoice lines found in XML payload.");
        }

        if (subtotal <= 0)
        {
            subtotal = invoiceLines.Sum(line => line.LineTotal);
        }

        if (totalAmount <= 0)
        {
            totalAmount = subtotal + taxAmount;
        }

        return new ParsedInvoice(
            InvoiceNumber: invoiceNumber.Trim(),
            SupplierCode: supplierCode,
            SupplierName: supplierName.Trim(),
            CurrencyCode: currencyCode.Trim().ToUpperInvariant(),
            InvoiceDate: ParseDateOnly(invoiceDateRaw),
            DueDate: ParseNullableDateOnly(dueDateRaw),
            Subtotal: subtotal,
            TaxAmount: taxAmount,
            TotalAmount: totalAmount,
            Lines: invoiceLines);
    }

    private static ParsedInvoiceLine ParseInvoiceLine(XElement lineElement, int fallbackLineNumber)
    {
        var lineNumber = ParseInt(GetFirstChildValue(lineElement, "ID"), fallbackLineNumber);

        var itemCode =
            GetPathValue(lineElement, "Item", "SellersItemIdentification", "ID")
            ?? GetPathValue(lineElement, "Item", "StandardItemIdentification", "ID")
            ?? GetPathValue(lineElement, "Item", "Name")
            ?? $"ITEM-{lineNumber}";

        var description =
            GetPathValue(lineElement, "Item", "Description")
            ?? GetPathValue(lineElement, "Item", "Name")
            ?? itemCode;

        var billedQuantity = ParseDecimal(GetFirstChildValue(lineElement, "InvoicedQuantity"));
        var unitPrice = ParseDecimal(GetPathValue(lineElement, "Price", "PriceAmount"));
        var lineTotal = ParseDecimal(GetFirstChildValue(lineElement, "LineExtensionAmount"));

        if (lineTotal <= 0)
        {
            lineTotal = billedQuantity * unitPrice;
        }

        var taxRate = ParseDecimal(GetPathValue(lineElement, "Item", "ClassifiedTaxCategory", "Percent"));

        return new ParsedInvoiceLine(
            LineNumber: lineNumber,
            ItemCode: itemCode.Trim().ToUpperInvariant(),
            Description: description.Trim(),
            BilledQuantity: billedQuantity,
            UnitPrice: unitPrice,
            TaxRate: taxRate,
            LineTotal: lineTotal);
    }

    private static string? GetFirstChildValue(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string? GetPathValue(XElement parent, params string[] localNames)
    {
        XElement? current = parent;
        foreach (var localName in localNames)
        {
            current = current?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
            if (current is null)
            {
                return null;
            }
        }

        return current.Value;
    }

    private static string? GetAttributeValue(XElement? element, string attributeName)
        => element?.Attributes().FirstOrDefault(attr => attr.Name.LocalName == attributeName)?.Value;

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static DateOnly ParseDateOnly(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static DateOnly? ParseNullableDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private async Task<MappingApplicationResult> ApplySupplierMappingAsync(
        ParsedInvoice parsedInvoice,
        Supplier supplier,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.SupplierMappingProfiles
            .AsNoTracking()
            .Include(item => item.ItemMappings)
            .SingleOrDefaultAsync(item => item.SupplierId == supplier.Id && item.IsActive, cancellationToken);

        if (profile is null)
        {
            return new MappingApplicationResult(
                parsedInvoice,
                [],
                null,
                0,
                parsedInvoice.Lines.Count);
        }

        var activeMappings = profile.ItemMappings
            .Where(mapping => mapping.IsActive)
            .ToDictionary(
                mapping => NormalizeCode(mapping.ExternalItemCode),
                mapping => mapping,
                StringComparer.OrdinalIgnoreCase);

        var rules = new SupplierMappingProfileRules(
            profile.RequireMappedItems,
            profile.DefaultTaxRate,
            profile.ItemMappings
                .Where(mapping => mapping.IsActive)
                .Select(mapping => new SupplierItemMappingRule(
                    mapping.ExternalItemCode,
                    mapping.InternalItemCode,
                    mapping.OverrideDescription,
                    mapping.OverrideTaxRate,
                    mapping.IsActive))
                .ToList());

        var mappingResult = SupplierMappingEngine.Apply(
            rules,
            parsedInvoice.Lines
                .Select(line => new SupplierMappingLine(
                    line.ItemCode,
                    line.Description,
                    line.TaxRate))
                .ToList());

        var diagnostics = new List<ImportDiagnostic>();
        if (profile.RequireMappedItems)
        {
            for (var index = 0; index < parsedInvoice.Lines.Count; index++)
            {
                var line = parsedInvoice.Lines[index];
                var normalizedCode = NormalizeCode(line.ItemCode);
                if (!activeMappings.ContainsKey(normalizedCode))
                {
                    diagnostics.Add(new ImportDiagnostic(
                        $"Invoice/InvoiceLine[{index + 1}]/Item",
                        $"Missing mapping for external item code '{line.ItemCode}'.",
                        "Error"));
                }
            }
        }

        if (diagnostics.Count == 0 && mappingResult.Errors.Count > 0)
        {
            diagnostics.AddRange(mappingResult.Errors.Select((error, index) =>
                new ImportDiagnostic($"Invoice/InvoiceLine[{index + 1}]", error, "Error")));
        }

        var remappedLines = parsedInvoice.Lines
            .Zip(mappingResult.Lines, (original, mapped) => original with
            {
                ItemCode = mapped.ItemCode,
                Description = mapped.Description,
                TaxRate = mapped.TaxRate
            })
            .ToList();

        return new MappingApplicationResult(
            parsedInvoice with { Lines = remappedLines },
            diagnostics,
            profile.Id,
            mappingResult.MappedLineCount,
            mappingResult.UnmappedLineCount);
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed record ImportDiagnostic(string Path, string Message, string Severity);

    private sealed record MappingApplicationResult(
        ParsedInvoice ParsedInvoice,
        IReadOnlyList<ImportDiagnostic> Diagnostics,
        int? ProfileId,
        int MappedLineCount,
        int UnmappedLineCount);

    private sealed record ParsedInvoice(
        string InvoiceNumber,
        string? SupplierCode,
        string SupplierName,
        string CurrencyCode,
        DateOnly InvoiceDate,
        DateOnly? DueDate,
        decimal Subtotal,
        decimal TaxAmount,
        decimal TotalAmount,
        IReadOnlyList<ParsedInvoiceLine> Lines);

    private sealed record ParsedInvoiceLine(
        int LineNumber,
        string ItemCode,
        string Description,
        decimal BilledQuantity,
        decimal UnitPrice,
        decimal TaxRate,
        decimal LineTotal);
}
