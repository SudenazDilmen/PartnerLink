using System.Security.Claims;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Services;

namespace PartnerLink.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The request has no authenticated user.");
}

/// <summary>Resolves (once per request) the organization that belongs to the signed-in user.</summary>
public sealed class CurrentOrganization(IHttpContextAccessor accessor, IOrganizationService organizations)
{
    private Organization? _organization;
    private bool _loaded;

    public async Task<Organization?> GetAsync(CancellationToken ct = default)
    {
        if (_loaded) return _organization;

        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _organization = userId is null ? null : await organizations.GetByOwnerAsync(userId, ct);
        _loaded = true;
        return _organization;
    }
}
