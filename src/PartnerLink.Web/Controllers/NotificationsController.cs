using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;

namespace PartnerLink.Web.Controllers;

[Authorize]
public class NotificationsController(INotificationService notifications) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var items = await notifications.ListAsync(userId, 50, ct);

        // The list keeps showing which ones were new; opening the page marks them read.
        await notifications.MarkAllReadAsync(userId, ct);
        return View(items);
    }
}
