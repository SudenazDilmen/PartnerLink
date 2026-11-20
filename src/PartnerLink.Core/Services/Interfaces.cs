using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;

namespace PartnerLink.Core.Services;

public interface IOrganizationService
{
    Task<Organization?> GetByOwnerAsync(string userId, CancellationToken ct = default);
    Task<Organization?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> OidExistsAsync(string oidCode, int? exceptOrganizationId = null, CancellationToken ct = default);
    Task<Organization> CreateAsync(string ownerUserId, OrganizationInput input, CancellationToken ct = default);
    Task<OperationResult> UpdateAsync(int id, OrganizationInput input, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> GetByStatusAsync(VerificationStatus status, CancellationToken ct = default);
    Task<OperationResult> SetVerificationAsync(int id, VerificationStatus status, CancellationToken ct = default);
    Task<OrganizationStats> GetStatsAsync(int organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> GetReviewsAsync(int organizationId, int take = 20, CancellationToken ct = default);
}

public interface IProjectService
{
    Task<PagedResult<ProjectCard>> SearchAsync(ProjectFilter filter, CancellationToken ct = default);
    Task<Project?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default);
    Task<OperationResult<Project>> CreateAsync(int organizationId, ProjectInput input, CancellationToken ct = default);
    Task<OperationResult> UpdateAsync(int id, int organizationId, ProjectInput input, CancellationToken ct = default);
    Task<OperationResult> CloseAsync(int id, int organizationId, CancellationToken ct = default);
}

public interface IApplicationService
{
    Task<OperationResult<PartnerApplication>> ApplyAsync(int projectId, int applicantOrganizationId, string message, CancellationToken ct = default);
    Task<OperationResult> RespondAsync(int applicationId, int ownerOrganizationId, bool accept, CancellationToken ct = default);
    Task<OperationResult> CompleteAsync(int applicationId, int organizationId, CancellationToken ct = default);
    /// <summary>Returns the application only when the organization is one of its two participants.</summary>
    Task<PartnerApplication?> GetForParticipantAsync(int applicationId, int organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PartnerApplication>> GetIncomingAsync(int organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PartnerApplication>> GetOutgoingAsync(int organizationId, CancellationToken ct = default);
    Task<PartnerApplication?> FindExistingAsync(int projectId, int applicantOrganizationId, CancellationToken ct = default);
}

public interface IChatService
{
    Task<bool> CanAccessAsync(int applicationId, int organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(int applicationId, int organizationId, CancellationToken ct = default);
    Task<OperationResult<ChatMessage>> SendAsync(int applicationId, int senderOrganizationId, string text, CancellationToken ct = default);
}

public interface IReviewService
{
    Task<bool> CanReviewAsync(int applicationId, int organizationId, CancellationToken ct = default);
    Task<OperationResult> CreateAsync(int applicationId, int reviewerOrganizationId, ReviewInput input, CancellationToken ct = default);
}

public interface ITrustService
{
    Task<decimal> CalculateAsync(int organizationId, CancellationToken ct = default);
    Task RecalculateAsync(int organizationId, CancellationToken ct = default);
    Task RecalculateAllAsync(CancellationToken ct = default);
    /// <summary>Flags accepted partnerships where the last message has gone unanswered for longer than the threshold.</summary>
    Task<int> DetectGhostingAsync(TimeSpan threshold, CancellationToken ct = default);
}

public interface IMatchService
{
    Task<IReadOnlyList<SuggestedPartner>> SuggestAsync(int projectId, int ownerOrganizationId, int take = 3, CancellationToken ct = default);
    Task<OperationResult> InviteAsync(int projectId, int ownerOrganizationId, int invitedOrganizationId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task AddAsync(string userId, string text, string url, CancellationToken ct = default);
    /// <summary>Adds a notification unless the user already has an unread one for the same url.</summary>
    Task AddUniqueUnreadAsync(string userId, string text, string url, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListAsync(string userId, int take = 50, CancellationToken ct = default);
    Task<int> UnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(string userId, int organizationId, CancellationToken ct = default);
}
