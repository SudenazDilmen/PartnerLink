using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Services;
using PartnerLink.Web.Extensions;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

[Authorize(Roles = Roles.Organization)]
public class ChatController(
    IApplicationService applications,
    IChatService chat,
    IReviewService reviews,
    CurrentOrganization current) : Controller
{
    /// <summary>The private chat room of an accepted partnership (messages arrive live through SignalR).</summary>
    public async Task<IActionResult> Room(int id, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Forbid();

        var application = await applications.GetForParticipantAsync(id, me.Id, ct);
        if (application is null ||
            application.Status is not (ApplicationStatus.Accepted or ApplicationStatus.Completed))
            return NotFound();

        var other = application.ApplicantOrganizationId == me.Id
            ? application.Project!.Organization!
            : application.Applicant!;

        var messages = await chat.GetMessagesAsync(id, me.Id, ct);
        var canReview = await reviews.CanReviewAsync(id, me.Id, ct);

        return View(new ChatRoomViewModel(
            application, me, other, messages,
            CanSend: application.Status == ApplicationStatus.Accepted,
            CanComplete: application.Status == ApplicationStatus.Accepted,
            CanReview: canReview));
    }
}
