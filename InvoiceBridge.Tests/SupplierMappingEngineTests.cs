using InvoiceBridge.Application.Workflow;

namespace InvoiceBridge.Tests;

public sealed class SupplierMappingEngineTests
{
    [Fact]
    public void Apply_MapsConfiguredExternalCodes()
    {
        var rules = new SupplierMappingProfileRules(
            RequireMappedItems: false,
            DefaultTaxRate: 5m,
            ItemMappings:
            [
                new SupplierItemMappingRule("EXT-ITEM-1", "INT-ITEM-1", "Mapped Item", 7.5m)
            ]);

        var result = SupplierMappingEngine.Apply(
            rules,
            [new SupplierMappingLine("ext-item-1", "Original", 0m)]);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.MappedLineCount);
        Assert.Equal("INT-ITEM-1", result.Lines[0].ItemCode);
        Assert.Equal("Mapped Item", result.Lines[0].Description);
        Assert.Equal(7.5m, result.Lines[0].TaxRate);
    }

    [Fact]
    public void Apply_UsesDefaultTaxRate_ForUnmappedLine()
    {
        var rules = new SupplierMappingProfileRules(
            RequireMappedItems: false,
            DefaultTaxRate: 12.5m,
            ItemMappings: []);

        var result = SupplierMappingEngine.Apply(
            rules,
            [new SupplierMappingLine("external-code", "Description", 0m)]);

        Assert.Empty(result.Errors);
        Assert.Equal(0, result.MappedLineCount);
        Assert.Equal(1, result.UnmappedLineCount);
        Assert.Equal(12.5m, result.Lines[0].TaxRate);
    }

    [Fact]
    public void Apply_EmitsErrors_WhenRequireMappedItemsAndLineNotMapped()
    {
        var rules = new SupplierMappingProfileRules(
            RequireMappedItems: true,
            DefaultTaxRate: null,
            ItemMappings: []);

        var result = SupplierMappingEngine.Apply(
            rules,
            [new SupplierMappingLine("MISSING-CODE", "Description", 3m)]);

        Assert.Single(result.Errors);
        Assert.Contains("MISSING-CODE", result.Errors[0]);
        Assert.Equal(1, result.UnmappedLineCount);
    }
}
