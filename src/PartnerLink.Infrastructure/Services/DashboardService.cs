using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class DashboardService(AppDbContext db, INotificationService notifications) : IDashboardService
{
    public async Task<DashboardSummary> GetAsync(string userId, int organizationId, CancellationToken ct = default)
    {
        var openProjects = await db.Projects.CountAsync(
            p => p.OrganizationId == organizationId && p.Status == ProjectStatus.Open, ct);

        var pendingIncoming = await db.Applications.CountAsync(
            a => a.Project!.OrganizationId == organizationId && a.Status == ApplicationStatus.Pending, ct);

        var activePartnerships = await db.Applications.CountAsync(
            a => a.Status == ApplicationStatus.Accepted &&
                 (a.ApplicantOrganizationId == organizationId || a.Project!.OrganizationId == organizationId), ct);

        var unread = await notifications.UnreadCountAsync(userId, ct);

        return new DashboardSummary(openProjects, pendingIncoming, activePartnerships, unread);
    }
}
