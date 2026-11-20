using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class MatchService(AppDbContext db, INotificationService notifications) : IMatchService
{
    public async Task<IReadOnlyList<SuggestedPartner>> SuggestAsync(
        int projectId, int ownerOrganizationId, int take = 3, CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == ownerOrganizationId, ct);
        if (project is null) return Array.Empty<SuggestedPartner>();

        var alreadyApplied = await db.Applications
            .Where(a => a.ProjectId == projectId)
            .Select(a => a.ApplicantOrganizationId)
            .ToListAsync(ct);

        // Only verified organizations that switched on "open to new projects" are suggested.
        var candidates = await db.Organizations
            .AsNoTracking()
            .Where(o => o.Id != ownerOrganizationId &&
                        o.Verification == VerificationStatus.Verified &&
                        o.OpenToNewProjects)
            .ToListAsync(ct);

        return candidates
            .Where(o => !alreadyApplied.Contains(o.Id))
            .Select(o => (Organization: o, Result: MatchScoring.Score(project, o)))
            .Where(x => x.Result.TopicMatch)
            .OrderByDescending(x => x.Result.Score)
            .ThenBy(x => x.Organization.Name)
            .Take(take)
            .Select(x => new SuggestedPartner(
                x.Organization.Id,
                x.Organization.Name,
                x.Organization.Country,
                x.Organization.TrustScore,
                x.Result.Score,
                x.Result.Reasons))
            .ToList();
    }

    public async Task<OperationResult> InviteAsync(
        int projectId, int ownerOrganizationId, int invitedOrganizationId, CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking()
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == ownerOrganizationId, ct);
        if (project is null) return OperationResult.Fail("Project not found.");

        var invited = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == invitedOrganizationId, ct);
        if (invited is null || invited.Id == ownerOrganizationId || invited.Verification != VerificationStatus.Verified)
            return OperationResult.Fail("This organization cannot be invited.");

        await notifications.AddUniqueUnreadAsync(
            invited.OwnerId,
            $"{project.Organization!.Name} invites you to join \"{project.Title}\".",
            $"/Projects/Details/{project.Id}", ct);

        return OperationResult.Ok();
    }
}
