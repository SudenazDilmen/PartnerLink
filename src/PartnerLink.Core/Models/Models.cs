using PartnerLink.Core.Enums;

namespace PartnerLink.Core.Models;

public sealed record OperationResult(bool Succeeded, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

public sealed record OperationResult<T>(bool Succeeded, T? Value = default, string? Error = null)
{
    public static OperationResult<T> Ok(T value) => new(true, value);
    public static OperationResult<T> Fail(string error) => new(false, default, error);
}

public sealed record OrganizationInput(
    string Name,
    string OidCode,
    string Country,
    string City,
    OrganizationType Type,
    string? Website,
    string? Description,
    Sector Sectors,
    Priority Priorities,
    bool OpenToNewProjects);

public sealed record ProjectInput(
    string Title,
    string Summary,
    string? Details,
    ActionType ActionType,
    Sector Sector,
    Priority Priorities,
    string TargetCountries,
    DateTime? ProjectDeadline,
    DateTime PartnerSearchEnd,
    string? PartnerRole,
    string? BudgetNote);

public sealed record ReviewInput(int Communication, int TaskManagement, int Reporting, string? Comment);

public sealed class ProjectFilter
{
    public string? Query { get; set; }
    public string? Country { get; set; }
    public ActionType? ActionType { get; set; }
    public Sector? Sector { get; set; }
    public Priority? Priority { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record ProjectCard(
    int Id,
    string Title,
    string Summary,
    ActionType ActionType,
    Sector Sector,
    Priority Priorities,
    DateTime PartnerSearchEnd,
    string TargetCountries,
    int OrganizationId,
    string OrganizationName,
    string OrganizationCountry,
    bool OrganizationVerified,
    decimal OrganizationTrust);

public sealed record OrganizationStats(
    double AverageRating,
    int ReviewCount,
    int CompletedPartnerships,
    int GhostedConversations);

public sealed record SuggestedPartner(
    int OrganizationId,
    string Name,
    string Country,
    decimal TrustScore,
    int Score,
    IReadOnlyList<string> Reasons);

public sealed record DashboardSummary(
    int OpenProjects,
    int PendingIncomingApplications,
    int ActivePartnerships,
    int UnreadNotifications);
