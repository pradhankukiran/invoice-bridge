using Microsoft.Extensions.Options;

namespace InvoiceBridge.Web.Security;

public interface IDemoUserStore
{
    DemoAuthUser? ValidateCredentials(string username, string password);
}

internal sealed class DemoUserStore(IOptions<DemoAuthOptions> options) : IDemoUserStore
{
    private readonly IReadOnlyDictionary<string, DemoAuthUser> _users = BuildLookup(options.Value.Users);

    public DemoAuthUser? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUsername = username.Trim();
        if (!_users.TryGetValue(normalizedUsername, out var user))
        {
            return null;
        }

        return string.Equals(user.Password, password, StringComparison.Ordinal)
            ? user
            : null;
    }

    private static IReadOnlyDictionary<string, DemoAuthUser> BuildLookup(IEnumerable<DemoAuthUser> users)
    {
        return users
            .Where(user => !string.IsNullOrWhiteSpace(user.Username))
            .GroupBy(user => user.Username.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
    }
}
