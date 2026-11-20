using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Services;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController(IOrganizationService organizations) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new AdminViewModel(
            await organizations.GetByStatusAsync(VerificationStatus.Pending, ct),
            await organizations.GetByStatusAsync(VerificationStatus.Verified, ct),
            await organizations.GetByStatusAsync(VerificationStatus.Rejected, ct)));

    [HttpPost]
    public Task<IActionResult> Verify(int id, CancellationToken ct) => SetAsync(id, VerificationStatus.Verified, ct);

    [HttpPost]
    public Task<IActionResult> Reject(int id, CancellationToken ct) => SetAsync(id, VerificationStatus.Rejected, ct);

    private async Task<IActionResult> SetAsync(int id, VerificationStatus status, CancellationToken ct)
    {
        var result = await organizations.SetVerificationAsync(id, status, ct);
        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded ? $"Organization marked as {status}." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
