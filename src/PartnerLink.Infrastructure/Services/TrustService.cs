using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

/// <summary>
/// TrustScore (0-5) = average review score (3.00 when there are no reviews yet) minus 0.5 for every
/// conversation in which the organization stopped answering ("ghosting").
/// </summary>
public sealed class TrustService(AppDbContext db, TimeProvider clock, INotificationService notifications) : ITrustService
{
    public const decimal DefaultScore = 3.00m;
    public const decimal GhostingPenalty = 0.50m;

    /// <summary>The formula on its own, so it can be unit tested without a database.</summary>
    public static decimal Compute(double? averageReview, int ghostedConversations)
    {
        var baseScore = averageReview.HasValue ? (decimal)averageReview.Value : DefaultScore;
        var score = baseScore - GhostingPenalty * ghostedConversations;
        return Math.Round(Math.Clamp(score, 0m, 5m), 2, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> CalculateAsync(int organizationId, CancellationToken ct = default)
    {
        var average = await db.Reviews
            .Where(r => r.ReviewedOrganizationId == organizationId)
            .Select(r => (double?)((r.Communication + r.TaskManagement + r.Reporting) / 3.0))
            .AverageAsync(ct);

        var ghosted = await db.Applications.CountAsync(a => a.GhostedOrganizationId == organizationId, ct);
        return Compute(average, ghosted);
    }

    public async Task RecalculateAsync(int organizationId, CancellationToken ct = default)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (organization is null) return;

        organization.TrustScore = await CalculateAsync(organizationId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RecalculateAllAsync(CancellationToken ct = default)
    {
        var ids = await db.Organizations.Select(o => o.Id).ToListAsync(ct);
        foreach (var id in ids)
            await RecalculateAsync(id, ct);
    }

    public async Task<int> DetectGhostingAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now - threshold;

        var candidates = await db.Applications
            .Include(a => a.Applicant)
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .Where(a => a.Status == ApplicationStatus.Accepted && a.GhostedAtUtc == null)
            .ToListAsync(ct);

        var affected = new HashSet<int>();
        var flagged = 0;

        foreach (var application in candidates)
        {
            var last = await db.ChatMessages
                .Where(m => m.ApplicationId == application.Id)
                .OrderByDescending(m => m.SentAtUtc)
                .FirstOrDefaultAsync(ct);

            // Nobody has written yet, or the last message is still fresh: nothing to flag.
            if (last is null || last.SentAtUtc > cutoff) continue;

            var ownerOrg = application.Project!.Organization!;
            var applicantOrg = application.Applicant!;

            // The silent party is the one who did NOT write the last message.
            var ghost = last.SenderOrganizationId == ownerOrg.Id ? applicantOrg : ownerOrg;

            application.GhostedOrganizationId = ghost.Id;
            application.GhostedAtUtc = now;
            affected.Add(ghost.Id);
            flagged++;

            await notifications.AddAsync(ghost.OwnerId,
                $"You have not replied in the chat about \"{application.Project.Title}\" for {(int)threshold.TotalDays} days. This lowers your trust score.",
                $"/Chat/Room/{application.Id}", ct);
        }

        await db.SaveChangesAsync(ct);

        foreach (var organizationId in affected)
            await RecalculateAsync(organizationId, ct);

        return flagged;
    }
}
