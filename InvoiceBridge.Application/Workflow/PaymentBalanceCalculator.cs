namespace InvoiceBridge.Application.Workflow;

public static class PaymentBalanceCalculator
{
    public static decimal Outstanding(decimal invoiceTotal, decimal paidAmount)
    {
        var outstanding = invoiceTotal - paidAmount;
        return outstanding < 0 ? 0 : outstanding;
    }

    public static bool IsPaid(decimal invoiceTotal, decimal paidAmount)
    {
        return paidAmount >= invoiceTotal;
    }
}
