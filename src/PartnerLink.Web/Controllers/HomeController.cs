using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

public class HomeController(IDashboardService dashboard, CurrentOrganization current) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return View("Landing");

        if (User.IsInRole(Roles.Admin))
            return RedirectToAction("Index", "Admin");

        var organization = await current.GetAsync(ct);
        if (organization is null)
            return View("Landing");

        var summary = await dashboard.GetAsync(User.GetUserId(), organization.Id, ct);
        return View(new HomeViewModel(organization, summary));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
