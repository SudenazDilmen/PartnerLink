using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core.Services;

namespace PartnerLink.Web.Components;

public class NotificationBadgeViewComponent(INotificationService notifications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = userId is null ? 0 : await notifications.UnreadCountAsync(userId);
        return View(count);
    }
}
