using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.Matching;

public static class MatchRuleEvaluator
{
    public static MatchResultCode Evaluate(
        decimal billedQuantity,
        decimal baselineQuantity,
        decimal billedUnitPrice,
        decimal expectedUnitPrice,
        decimal billedTaxRate,
        decimal expectedTaxRate,
        bool hasGoodsReceipt,
        decimal quantityTolerance = 0.02m,
        decimal priceTolerance = 0.01m,
        decimal taxTolerance = 0.25m)
    {
        if (!hasGoodsReceipt)
        {
            return MatchResultCode.MissingGoodsReceipt;
        }

        var quantityDelta = Math.Abs(billedQuantity - baselineQuantity);
        var quantityThreshold = Math.Max(1m, baselineQuantity) * quantityTolerance;
        if (quantityDelta > quantityThreshold)
        {
            return MatchResultCode.QuantityVariance;
        }

        var priceDelta = Math.Abs(billedUnitPrice - expectedUnitPrice);
        var priceThreshold = Math.Max(1m, expectedUnitPrice) * priceTolerance;
        if (priceDelta > priceThreshold)
        {
            return MatchResultCode.PriceVariance;
        }

        var taxDelta = Math.Abs(billedTaxRate - expectedTaxRate);
        if (taxDelta > taxTolerance)
        {
            return MatchResultCode.TaxVariance;
        }

        return MatchResultCode.Matched;
    }
}
