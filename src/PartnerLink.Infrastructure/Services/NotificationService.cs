using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class NotificationService(AppDbContext db, TimeProvider clock) : INotificationService
{
    public async Task AddAsync(string userId, string text, string url, CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Text = text.Length > 500 ? text[..500] : text,
            Url = url,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddUniqueUnreadAsync(string userId, string text, string url, CancellationToken ct = default)
    {
        var exists = await db.Notifications.AnyAsync(n => n.UserId == userId && n.Url == url && !n.IsRead, ct);
        if (!exists)
            await AddAsync(userId, text, url, ct);
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(string userId, int take = 50, CancellationToken ct = default) =>
        await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        if (unread.Count == 0) return;

        foreach (var notification in unread)
            notification.IsRead = true;
        await db.SaveChangesAsync(ct);
    }
}
