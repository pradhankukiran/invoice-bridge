namespace InvoiceBridge.Web.Security;

public sealed class DemoAuthOptions
{
    public List<DemoAuthUser> Users { get; set; } = [];
}

public sealed class DemoAuthUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}
