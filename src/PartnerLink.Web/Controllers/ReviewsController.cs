using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize(Roles = Roles.Organization)]
public class ReviewsController(
    IReviewService reviews,
    IApplicationService applications,
    CurrentOrganization current) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int id, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        if (!await reviews.CanReviewAsync(id, me.Id, ct))
        {
            TempData["Error"] = "This partnership cannot be reviewed (not completed yet, or you already reviewed it).";
            return RedirectToAction("Mine", "Projects");
        }

        var model = await BuildAsync(id, me.Id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int id, ReviewFormViewModel model, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        if (!ModelState.IsValid)
        {
            var fresh = await BuildAsync(id, me.Id, ct);
            if (fresh is null) return NotFound();
            model.OtherOrganizationName = fresh.OtherOrganizationName;
            model.ProjectTitle = fresh.ProjectTitle;
            return View(model);
        }

        var result = await reviews.CreateAsync(id, me.Id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "Projects");
        }

        TempData["Success"] = "Thank you! Your review helps the community choose reliable partners.";
        return RedirectToAction("Room", "Chat", new { id });
    }

    private async Task<ReviewFormViewModel?> BuildAsync(int applicationId, int myOrganizationId, CancellationToken ct)
    {
        var application = await applications.GetForParticipantAsync(applicationId, myOrganizationId, ct);
        if (application is null) return null;

        var other = application.ApplicantOrganizationId == myOrganizationId
            ? application.Project!.Organization!
            : application.Applicant!;

        return new ReviewFormViewModel
        {
            ApplicationId = applicationId,
            OtherOrganizationName = other.Name,
            ProjectTitle = application.Project!.Title
        };
    }
}
