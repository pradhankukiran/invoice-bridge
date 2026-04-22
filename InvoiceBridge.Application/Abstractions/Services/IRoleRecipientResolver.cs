namespace InvoiceBridge.Application.Abstractions.Services;

public interface IRoleRecipientResolver
{
    Task<IReadOnlyList<string>> ResolveUsersByRoleAsync(string role, CancellationToken cancellationToken = default);
}
