using InvoiceBridge.Application.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace InvoiceBridge.Web.Security;

internal sealed class DemoRoleRecipientResolver(IOptions<DemoAuthOptions> options) : IRoleRecipientResolver
{
    public Task<IReadOnlyList<string>> ResolveUsersByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var normalizedRole = role.Trim();

        var recipients = options.Value.Users
            .Where(user => !string.IsNullOrWhiteSpace(user.Username))
            .Where(user => user.Roles.Any(userRole => string.Equals(userRole, normalizedRole, StringComparison.OrdinalIgnoreCase)))
            .Select(user => user.Username.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(recipients);
    }
}
