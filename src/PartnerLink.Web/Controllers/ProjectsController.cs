using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize]
public class ProjectsController(
    IProjectService projects,
    IApplicationService applications,
    IMatchService matching,
    IOrganizationService organizations,
    CurrentOrganization current) : Controller
{
    /// <summary>The partner feed: search and filter all open partner searches.</summary>
    public async Task<IActionResult> Index([FromQuery] ProjectFilter filter, CancellationToken ct) =>
        View(new ProjectIndexViewModel(filter, await projects.SearchAsync(filter, ct)));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var project = await projects.GetAsync(id, ct);
        if (project is null) return NotFound();

        var me = await current.GetAsync(ct);
        var isOwner = me is not null && me.Id == project.OrganizationId;

        var existing = me is not null && !isOwner
            ? await applications.FindExistingAsync(project.Id, me.Id, ct)
            : null;

        // Only the owner sees partner suggestions.
        var suggestions = isOwner
            ? await matching.SuggestAsync(project.Id, me!.Id, 3, ct)
            : Array.Empty<SuggestedPartner>();

        var hostStats = await organizations.GetStatsAsync(project.OrganizationId, ct);

        return View(new ProjectDetailsViewModel(project, me, isOwner, existing, suggestions, hostStats));
    }

    [Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        return View(await projects.GetByOrganizationAsync(me.Id, ct));
    }

    [HttpGet, Authorize(Roles = Roles.Organization)]
    public IActionResult Create() => View("Form", new ProjectFormViewModel
    {
        PartnerSearchEnd = DateTime.UtcNow.Date.AddDays(30)
    });

    [HttpPost, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Create(ProjectFormViewModel model, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();
        if (!ModelState.IsValid) return View("Form", model);

        var result = await projects.CreateAsync(me.Id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The project could not be created.");
            return View("Form", model);
        }

        TempData["Success"] = "Your partner search is live.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        var project = await projects.GetAsync(id, ct);
        if (me is null || project is null || project.OrganizationId != me.Id) return NotFound();

        return View("Form", ProjectFormViewModel.From(project));
    }

    [HttpPost, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Edit(int id, ProjectFormViewModel model, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();
        if (!ModelState.IsValid) return View("Form", model);

        var result = await projects.UpdateAsync(id, me.Id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The project could not be saved.");
            return View("Form", model);
        }

        TempData["Success"] = "Project updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        var result = await projects.CloseAsync(id, me.Id, ct);
        if (!result.Succeeded) return NotFound();

        TempData["Success"] = "The partner search is closed.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Invite(int id, int organizationId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        var result = await matching.InviteAsync(id, me.Id, organizationId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Invitation sent." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
