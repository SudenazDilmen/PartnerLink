using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PartnerLink.Core.Services;

namespace PartnerLink.Web.Hubs;

/// <summary>Real-time chat for an accepted partnership. One SignalR group per application (chat room).</summary>
[Authorize]
public class ChatHub(IChatService chat, IOrganizationService organizations) : Hub
{
    private static string Room(int applicationId) => $"application-{applicationId}";

    public async Task Join(int applicationId)
    {
        var organization = await GetOrganizationAsync();
        if (!await chat.CanAccessAsync(applicationId, organization.Id))
            throw new HubException("You are not allowed to join this chat.");

        await Groups.AddToGroupAsync(Context.ConnectionId, Room(applicationId));
    }

    public async Task Send(int applicationId, string text)
    {
        var organization = await GetOrganizationAsync();

        var result = await chat.SendAsync(applicationId, organization.Id, text);
        if (!result.Succeeded || result.Value is null)
            throw new HubException(result.Error ?? "The message could not be sent.");

        var message = result.Value;
        await Clients.Group(Room(applicationId)).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            senderOrganizationId = organization.Id,
            senderName = organization.Name,
            text = message.Text,
            sentAtUtc = message.SentAtUtc
        });
    }

    private async Task<PartnerLink.Core.Entities.Organization> GetOrganizationAsync()
    {
        var userId = Context.UserIdentifier ?? throw new HubException("Not signed in.");
        return await organizations.GetByOwnerAsync(userId)
               ?? throw new HubException("Your account has no organization.");
    }
}
