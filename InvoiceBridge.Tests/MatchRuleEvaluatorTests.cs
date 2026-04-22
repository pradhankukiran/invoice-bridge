using InvoiceBridge.Application.Matching;
using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Tests;

public sealed class MatchRuleEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsMatched_WhenWithinTolerance()
    {
        var result = MatchRuleEvaluator.Evaluate(
            billedQuantity: 100,
            baselineQuantity: 100.5m,
            billedUnitPrice: 10.00m,
            expectedUnitPrice: 10.05m,
            billedTaxRate: 5.00m,
            expectedTaxRate: 5.10m,
            hasGoodsReceipt: true);

        Assert.Equal(MatchResultCode.Matched, result);
    }

    [Fact]
    public void Evaluate_ReturnsMissingGoodsReceipt_WhenReceiptMissing()
    {
        var result = MatchRuleEvaluator.Evaluate(
            billedQuantity: 50,
            baselineQuantity: 50,
            billedUnitPrice: 12,
            expectedUnitPrice: 12,
            billedTaxRate: 5,
            expectedTaxRate: 5,
            hasGoodsReceipt: false);

        Assert.Equal(MatchResultCode.MissingGoodsReceipt, result);
    }

    [Fact]
    public void Evaluate_ReturnsQuantityVariance_WhenOutOfTolerance()
    {
        var result = MatchRuleEvaluator.Evaluate(
            billedQuantity: 120,
            baselineQuantity: 100,
            billedUnitPrice: 10,
            expectedUnitPrice: 10,
            billedTaxRate: 5,
            expectedTaxRate: 5,
            hasGoodsReceipt: true);

        Assert.Equal(MatchResultCode.QuantityVariance, result);
    }

    [Fact]
    public void Evaluate_ReturnsPriceVariance_WhenOutOfTolerance()
    {
        var result = MatchRuleEvaluator.Evaluate(
            billedQuantity: 100,
            baselineQuantity: 100,
            billedUnitPrice: 12,
            expectedUnitPrice: 10,
            billedTaxRate: 5,
            expectedTaxRate: 5,
            hasGoodsReceipt: true);

        Assert.Equal(MatchResultCode.PriceVariance, result);
    }
}
