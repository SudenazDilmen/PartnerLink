using Microsoft.EntityFrameworkCore;
using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class OrganizationService(AppDbContext db, TimeProvider clock, INotificationService notifications)
    : IOrganizationService
{
    public Task<Organization?> GetByOwnerAsync(string userId, CancellationToken ct = default) =>
        db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OwnerId == userId, ct);

    public Task<Organization?> GetAsync(int id, CancellationToken ct = default) =>
        db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<bool> OidExistsAsync(string oidCode, int? exceptOrganizationId = null, CancellationToken ct = default)
    {
        var oid = OidCode.Normalize(oidCode);
        return db.Organizations.AnyAsync(o => o.OidCode == oid && o.Id != (exceptOrganizationId ?? 0), ct);
    }

    public async Task<Organization> CreateAsync(string ownerUserId, OrganizationInput input, CancellationToken ct = default)
    {
        var organization = new Organization
        {
            OwnerId = ownerUserId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Verification = VerificationStatus.Pending
        };
        Apply(organization, input);

        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);
        return organization;
    }

    public async Task<OperationResult> UpdateAsync(int id, OrganizationInput input, CancellationToken ct = default)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (organization is null) return OperationResult.Fail("Organization not found.");

        var newOid = OidCode.Normalize(input.OidCode);
        if (await OidExistsAsync(newOid, id, ct))
            return OperationResult.Fail("Another organization already uses this OID.");

        // A changed OID has to be verified again.
        if (!string.Equals(organization.OidCode, newOid, StringComparison.Ordinal))
            organization.Verification = VerificationStatus.Pending;

        Apply(organization, input);
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<IReadOnlyList<Organization>> GetByStatusAsync(VerificationStatus status, CancellationToken ct = default) =>
        await db.Organizations
            .AsNoTracking()
            .Include(o => o.Owner)
            .Where(o => o.Verification == status)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<OperationResult> SetVerificationAsync(int id, VerificationStatus status, CancellationToken ct = default)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (organization is null) return OperationResult.Fail("Organization not found.");

        organization.Verification = status;
        await db.SaveChangesAsync(ct);

        var text = status == VerificationStatus.Verified
            ? "Your organization has been verified. You can now publish projects and apply to partner searches."
            : "Your organization could not be verified. Please check your OID and details.";
        await notifications.AddAsync(organization.OwnerId, text, $"/Organizations/Details/{id}", ct);

        return OperationResult.Ok();
    }

    public async Task<OrganizationStats> GetStatsAsync(int organizationId, CancellationToken ct = default)
    {
        var reviews = db.Reviews.Where(r => r.ReviewedOrganizationId == organizationId);

        var average = await reviews
            .Select(r => (double?)((r.Communication + r.TaskManagement + r.Reporting) / 3.0))
            .AverageAsync(ct);
        var reviewCount = await reviews.CountAsync(ct);

        var completed = await db.Applications.CountAsync(a =>
            a.Status == ApplicationStatus.Completed &&
            (a.ApplicantOrganizationId == organizationId || a.Project!.OrganizationId == organizationId), ct);

        var ghosted = await db.Applications.CountAsync(a => a.GhostedOrganizationId == organizationId, ct);

        return new OrganizationStats(average ?? 0, reviewCount, completed, ghosted);
    }

    public async Task<IReadOnlyList<Review>> GetReviewsAsync(int organizationId, int take = 20, CancellationToken ct = default) =>
        await db.Reviews
            .AsNoTracking()
            .Include(r => r.Reviewer)
            .Where(r => r.ReviewedOrganizationId == organizationId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    private static void Apply(Organization organization, OrganizationInput input)
    {
        organization.Name = input.Name.Trim();
        organization.OidCode = OidCode.Normalize(input.OidCode);
        organization.Country = input.Country.Trim();
        organization.City = input.City.Trim();
        organization.Type = input.Type;
        organization.Website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim();
        organization.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        organization.Sectors = input.Sectors;
        organization.Priorities = input.Priorities;
        organization.OpenToNewProjects = input.OpenToNewProjects;
    }
}
