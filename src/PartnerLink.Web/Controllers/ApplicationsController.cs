using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize(Roles = Roles.Organization)]
public class ApplicationsController(IApplicationService applications, CurrentOrganization current) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Apply(ApplyViewModel model, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please write a short message (up to 2000 characters).";
            return RedirectToAction("Details", "Projects", new { id = model.ProjectId });
        }

        var result = await applications.ApplyAsync(model.ProjectId, me.Id, model.Message, ct);
        if (result.Succeeded)
            TempData["Success"] = "Application sent. You will be notified when the organizer answers.";
        else
            TempData["Error"] = result.Error;

        return RedirectToAction("Details", "Projects", new { id = model.ProjectId });
    }

    /// <summary>Applications to my projects.</summary>
    public async Task<IActionResult> Incoming(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        return View(await applications.GetIncomingAsync(me.Id, ct));
    }

    /// <summary>Applications I sent.</summary>
    public async Task<IActionResult> Outgoing(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        return View(await applications.GetOutgoingAsync(me.Id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Respond(int id, bool accept, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        var result = await applications.RespondAsync(id, me.Id, accept, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Incoming));
        }

        if (accept)
        {
            TempData["Success"] = "Application accepted. A private chat room is now open.";
            return RedirectToAction("Room", "Chat", new { id });
        }

        TempData["Success"] = "Application declined.";
        return RedirectToAction(nameof(Incoming));
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        var result = await applications.CompleteAsync(id, me.Id, ct);
        if (result.Succeeded)
            TempData["Success"] = "Partnership marked as completed. Please leave a review.";
        else
            TempData["Error"] = result.Error;

        return result.Succeeded
            ? RedirectToAction("Create", "Reviews", new { id })
            : RedirectToAction("Room", "Chat", new { id });
    }
}
