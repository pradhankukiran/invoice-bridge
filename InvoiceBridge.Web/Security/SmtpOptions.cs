using System.ComponentModel.DataAnnotations;

namespace InvoiceBridge.Web.Security;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromDisplayName { get; set; } = "InvoiceBridge";

    [Range(1_000, 120_000)]
    public int TimeoutMilliseconds { get; set; } = 15_000;
}
