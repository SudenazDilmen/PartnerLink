using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize]
public class OrganizationsController(IOrganizationService organizations, CurrentOrganization current) : Controller
{
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var organization = await organizations.GetAsync(id, ct);
        if (organization is null) return NotFound();

        var me = await current.GetAsync(ct);
        var stats = await organizations.GetStatsAsync(id, ct);
        var reviews = await organizations.GetReviewsAsync(id, 20, ct);

        return View(new OrganizationDetailsViewModel(organization, stats, reviews, me?.Id == id));
    }

    [HttpGet, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        return View(OrganizationFormViewModel.From(me));
    }

    [HttpPost, Authorize(Roles = Roles.Organization)]
    public async Task<IActionResult> Edit(OrganizationFormViewModel model, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();
        if (!ModelState.IsValid) return View(model);

        var result = await organizations.UpdateAsync(me.Id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The profile could not be saved.");
            return View(model);
        }

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Details), new { id = me.Id });
    }
}
