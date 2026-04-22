namespace InvoiceBridge.Web.Security;

public static class AuthorizationPolicies
{
    public const string AnyAuthenticatedUser = "AnyAuthenticatedUser";
    public const string ProcurementAccess = "ProcurementAccess";
    public const string WarehouseAccess = "WarehouseAccess";
    public const string ImportAccess = "ImportAccess";
    public const string IntegrationAccess = "IntegrationAccess";
    public const string InvoiceReviewAccess = "InvoiceReviewAccess";
    public const string ApprovalAccess = "ApprovalAccess";
    public const string ExceptionAccess = "ExceptionAccess";
    public const string ExportAccess = "ExportAccess";
    public const string PaymentAccess = "PaymentAccess";
    public const string AuditAccess = "AuditAccess";
}
