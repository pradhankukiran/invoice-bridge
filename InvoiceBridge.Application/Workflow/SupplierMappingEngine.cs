namespace InvoiceBridge.Application.Workflow;

public sealed record SupplierMappingProfileRules(
    bool RequireMappedItems,
    decimal? DefaultTaxRate,
    IReadOnlyList<SupplierItemMappingRule> ItemMappings);

public sealed record SupplierItemMappingRule(
    string ExternalItemCode,
    string InternalItemCode,
    string? OverrideDescription,
    decimal? OverrideTaxRate,
    bool IsActive = true);

public sealed record SupplierMappingLine(
    string ItemCode,
    string Description,
    decimal TaxRate);

public sealed class SupplierMappingResult
{
    public required IReadOnlyList<SupplierMappingLine> Lines { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public int MappedLineCount { get; init; }
    public int UnmappedLineCount { get; init; }
}

public static class SupplierMappingEngine
{
    public static SupplierMappingResult Apply(SupplierMappingProfileRules rules, IReadOnlyList<SupplierMappingLine> lines)
    {
        var errors = new List<string>();
        var mappedLines = new List<SupplierMappingLine>(lines.Count);

        var mappings = rules.ItemMappings
            .Where(mapping => mapping.IsActive)
            .GroupBy(mapping => NormalizeCode(mapping.ExternalItemCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var mappedCount = 0;
        var unmappedCount = 0;

        foreach (var line in lines)
        {
            var normalizedCode = NormalizeCode(line.ItemCode);

            if (mappings.TryGetValue(normalizedCode, out var mapping))
            {
                mappedCount++;
                mappedLines.Add(new SupplierMappingLine(
                    ItemCode: NormalizeCode(mapping.InternalItemCode),
                    Description: string.IsNullOrWhiteSpace(mapping.OverrideDescription)
                        ? line.Description
                        : mapping.OverrideDescription.Trim(),
                    TaxRate: mapping.OverrideTaxRate ?? ResolveTaxRate(line.TaxRate, rules.DefaultTaxRate)));

                continue;
            }

            unmappedCount++;
            if (rules.RequireMappedItems)
            {
                errors.Add($"Missing mapping for external item code '{line.ItemCode}'.");
            }

            mappedLines.Add(new SupplierMappingLine(
                ItemCode: normalizedCode,
                Description: line.Description,
                TaxRate: ResolveTaxRate(line.TaxRate, rules.DefaultTaxRate)));
        }

        return new SupplierMappingResult
        {
            Lines = mappedLines,
            Errors = errors,
            MappedLineCount = mappedCount,
            UnmappedLineCount = unmappedCount
        };
    }

    private static decimal ResolveTaxRate(decimal existingTaxRate, decimal? defaultTaxRate)
    {
        if (existingTaxRate > 0)
        {
            return existingTaxRate;
        }

        return defaultTaxRate ?? 0;
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}
