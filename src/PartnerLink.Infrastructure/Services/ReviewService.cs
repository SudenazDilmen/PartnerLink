using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class ReviewService(
    AppDbContext db,
    TimeProvider clock,
    ITrustService trust,
    INotificationService notifications) : IReviewService
{
    public async Task<bool> CanReviewAsync(int applicationId, int organizationId, CancellationToken ct = default)
    {
        var participant = await db.Applications.AnyAsync(a =>
            a.Id == applicationId &&
            a.Status == ApplicationStatus.Completed &&
            (a.ApplicantOrganizationId == organizationId || a.Project!.OrganizationId == organizationId), ct);
        if (!participant) return false;

        var alreadyReviewed = await db.Reviews.AnyAsync(
            r => r.ApplicationId == applicationId && r.ReviewerOrganizationId == organizationId, ct);
        return !alreadyReviewed;
    }

    public async Task<OperationResult> CreateAsync(int applicationId, int reviewerOrganizationId, ReviewInput input, CancellationToken ct = default)
    {
        if (!IsScore(input.Communication) || !IsScore(input.TaskManagement) || !IsScore(input.Reporting))
            return OperationResult.Fail("Each score must be between 1 and 5.");

        if (!await CanReviewAsync(applicationId, reviewerOrganizationId, ct))
            return OperationResult.Fail("You cannot review this partnership (it is not completed, or you already reviewed it).");

        var application = await db.Applications
            .Include(a => a.Applicant)
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .FirstAsync(a => a.Id == applicationId, ct);

        var reviewerIsApplicant = application.ApplicantOrganizationId == reviewerOrganizationId;
        var reviewer = reviewerIsApplicant ? application.Applicant! : application.Project!.Organization!;
        var reviewed = reviewerIsApplicant ? application.Project!.Organization! : application.Applicant!;

        db.Reviews.Add(new Review
        {
            ApplicationId = applicationId,
            ReviewerOrganizationId = reviewer.Id,
            ReviewedOrganizationId = reviewed.Id,
            Communication = input.Communication,
            TaskManagement = input.TaskManagement,
            Reporting = input.Reporting,
            Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);

        await trust.RecalculateAsync(reviewed.Id, ct);
        await notifications.AddAsync(reviewed.OwnerId,
            $"{reviewer.Name} left a review about your organization.",
            $"/Organizations/Details/{reviewed.Id}", ct);

        return OperationResult.Ok();
    }

    private static bool IsScore(int value) => value is >= 1 and <= 5;
}
