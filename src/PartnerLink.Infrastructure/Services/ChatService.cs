using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class ChatService(AppDbContext db, TimeProvider clock, INotificationService notifications) : IChatService
{
    public const int MaxMessageLength = 2000;

    // A chat room exists only for accepted (or finished) partnerships, and only the two participants may enter.
    public Task<bool> CanAccessAsync(int applicationId, int organizationId, CancellationToken ct = default) =>
        db.Applications.AnyAsync(a =>
            a.Id == applicationId &&
            (a.Status == ApplicationStatus.Accepted || a.Status == ApplicationStatus.Completed) &&
            (a.ApplicantOrganizationId == organizationId || a.Project!.OrganizationId == organizationId), ct);

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(int applicationId, int organizationId, CancellationToken ct = default)
    {
        if (!await CanAccessAsync(applicationId, organizationId, ct))
            return Array.Empty<ChatMessage>();

        var messages = await db.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ApplicationId == applicationId)
            .OrderBy(m => m.SentAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        // Opening the room marks the other side's messages as read.
        var now = clock.GetUtcNow().UtcDateTime;
        var unread = messages.Where(m => m.SenderOrganizationId != organizationId && m.ReadAtUtc == null).ToList();
        if (unread.Count > 0)
        {
            foreach (var message in unread)
                message.ReadAtUtc = now;
            await db.SaveChangesAsync(ct);
        }

        return messages;
    }

    public async Task<OperationResult<ChatMessage>> SendAsync(int applicationId, int senderOrganizationId, string text, CancellationToken ct = default)
    {
        var body = text?.Trim() ?? string.Empty;
        if (body.Length == 0)
            return OperationResult<ChatMessage>.Fail("The message is empty.");
        if (body.Length > MaxMessageLength)
            return OperationResult<ChatMessage>.Fail($"The message is too long ({MaxMessageLength} characters max).");

        var application = await db.Applications
            .Include(a => a.Applicant)
            .Include(a => a.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application is null ||
            (application.ApplicantOrganizationId != senderOrganizationId && application.Project!.OrganizationId != senderOrganizationId))
            return OperationResult<ChatMessage>.Fail("Chat room not found.");

        if (application.Status != ApplicationStatus.Accepted)
            return OperationResult<ChatMessage>.Fail("This chat is read-only.");

        var senderIsApplicant = application.ApplicantOrganizationId == senderOrganizationId;
        var sender = senderIsApplicant ? application.Applicant! : application.Project!.Organization!;
        var recipient = senderIsApplicant ? application.Project!.Organization! : application.Applicant!;

        var message = new ChatMessage
        {
            ApplicationId = applicationId,
            SenderOrganizationId = senderOrganizationId,
            Sender = sender,
            Text = body,
            SentAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(ct);

        await notifications.AddUniqueUnreadAsync(
            recipient.OwnerId,
            $"New message from {sender.Name}",
            $"/Chat/Room/{applicationId}", ct);

        return OperationResult<ChatMessage>.Ok(message);
    }
}
