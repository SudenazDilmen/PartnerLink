using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class ApplicationService(AppDbContext db, TimeProvider clock, INotificationService notifications)
    : IApplicationService
{
    public async Task<OperationResult<PartnerApplication>> ApplyAsync(
        int projectId, int applicantOrganizationId, string message, CancellationToken ct = default)
    {
        var applicant = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == applicantOrganizationId, ct);
        if (applicant is null)
            return OperationResult<PartnerApplication>.Fail("Organization not found.");
        if (applicant.Verification != VerificationStatus.Verified)
            return OperationResult<PartnerApplication>.Fail("Your organization must be verified before you can apply.");

        var project = await db.Projects.Include(p => p.Organization).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return OperationResult<PartnerApplication>.Fail("Project not found.");
        if (project.OrganizationId == applicantOrganizationId)
            return OperationResult<PartnerApplication>.Fail("You cannot apply to your own project.");

        var today = clock.GetUtcNow().UtcDateTime.Date;
        if (project.Status != ProjectStatus.Open || project.PartnerSearchEnd < today)
            return OperationResult<PartnerApplication>.Fail("This partner search is closed.");

        var text = message?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return OperationResult<PartnerApplication>.Fail("Please introduce your organization in a short message.");
        if (text.Length > 2000)
            return OperationResult<PartnerApplication>.Fail("The message is too long (2000 characters max).");

        if (await db.Applications.AnyAsync(a => a.ProjectId == projectId && a.ApplicantOrganizationId == applicantOrganizationId, ct))
            return OperationResult<PartnerApplication>.Fail("You have already applied to this project.");

        var application = new PartnerApplication
        {
            ProjectId = projectId,
            ApplicantOrganizationId = applicantOrganizationId,
            Message = text,
            Status = ApplicationStatus.Pending,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);

        await notifications.AddAsync(
            project.Organization!.OwnerId,
            $"{applicant.Name} applied to \"{project.Title}\".",
            "/Applications/Incoming", ct);

        return OperationResult<PartnerApplication>.Ok(application);
    }

    public async Task<OperationResult> RespondAsync(int applicationId, int ownerOrganizationId, bool accept, CancellationToken ct = default)
    {
        var application = await LoadTrackedAsync(applicationId, ct);
        if (application is null || application.Project!.OrganizationId != ownerOrganizationId)
            return OperationResult.Fail("Application not found.");
        if (application.Status != ApplicationStatus.Pending)
            return OperationResult.Fail("This application has already been answered.");

        application.Status = accept ? ApplicationStatus.Accepted : ApplicationStatus.Rejected;
        application.RespondedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        var owner = application.Project.Organization!;
        if (accept)
            await notifications.AddAsync(application.Applicant!.OwnerId,
                $"{owner.Name} accepted your application to \"{application.Project.Title}\". You can chat now.",
                $"/Chat/Room/{application.Id}", ct);
        else
            await notifications.AddAsync(application.Applicant!.OwnerId,
                $"{owner.Name} declined your application to \"{application.Project.Title}\".",
                "/Applications/Outgoing", ct);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> CompleteAsync(int applicationId, int organizationId, CancellationToken ct = default)
    {
        var application = await LoadTrackedAsync(applicationId, ct);
        if (application is null || !IsParticipant(application, organizationId))
            return OperationResult.Fail("Partnership not found.");
        if (application.Status != ApplicationStatus.Accepted)
            return OperationResult.Fail("Only an active partnership can be marked as completed.");

        application.Status = ApplicationStatus.Completed;
        application.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        var otherOwnerId = application.ApplicantOrganizationId == organizationId
            ? application.Project!.Organization!.OwnerId
            : application.Applicant!.OwnerId;
        await notifications.AddAsync(otherOwnerId,
            $"The partnership on \"{application.Project!.Title}\" was marked as completed. You can now leave a review.",
            $"/Reviews/Create/{application.Id}", ct);

        return OperationResult.Ok();
    }

    public Task<PartnerApplication?> GetForParticipantAsync(int applicationId, int organizationId, CancellationToken ct = default) =>
        db.Applications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(a => a.Id == applicationId &&
                (a.ApplicantOrganizationId == organizationId || a.Project!.OrganizationId == organizationId), ct);

    public async Task<IReadOnlyList<PartnerApplication>> GetIncomingAsync(int organizationId, CancellationToken ct = default) =>
        await db.Applications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Project)
            .Where(a => a.Project!.OrganizationId == organizationId)
            .OrderBy(a => a.Status == ApplicationStatus.Pending ? 0 : 1)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PartnerApplication>> GetOutgoingAsync(int organizationId, CancellationToken ct = default) =>
        await db.Applications
            .AsNoTracking()
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .Where(a => a.ApplicantOrganizationId == organizationId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<PartnerApplication?> FindExistingAsync(int projectId, int applicantOrganizationId, CancellationToken ct = default) =>
        db.Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.ApplicantOrganizationId == applicantOrganizationId, ct);

    private Task<PartnerApplication?> LoadTrackedAsync(int applicationId, CancellationToken ct) =>
        db.Applications
            .Include(a => a.Applicant)
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

    private static bool IsParticipant(PartnerApplication application, int organizationId) =>
        application.ApplicantOrganizationId == organizationId || application.Project!.OrganizationId == organizationId;
}
