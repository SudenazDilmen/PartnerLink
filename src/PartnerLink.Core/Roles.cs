namespace PartnerLink.Core;

public static class Roles
{
    /// <summary>Platform administrator: verifies organizations.</summary>
    public const string Admin = "Admin";

    /// <summary>A registered organization account.</summary>
    public const string Organization = "Organization";

    public static readonly string[] All = [Admin, Organization];
}
