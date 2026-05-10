using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Notifications;

/// <summary>
/// Higher-level helpers used by services to fire notifications without
/// touching <see cref="WebPushService"/> directly. Today: web-push only.
/// Future: email + SMS routed through here.
/// </summary>
public class NotificationDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly WebPushService _push;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        WebPushService push,
        ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _users = users;
        _push = push;
        _logger = logger;
    }

    public async Task NotifyRoleAsync(string role, NotificationPayload payload, CancellationToken ct = default)
    {
        try
        {
            var users = await _users.GetUsersInRoleAsync(role);
            foreach (var u in users)
            {
                await _push.SendToUserAsync(u.Id, payload, ct);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "NotifyRoleAsync({Role}) failed", role); }
    }

    public async Task NotifyUserAsync(string userId, NotificationPayload payload, CancellationToken ct = default)
    {
        try { await _push.SendToUserAsync(userId, payload, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "NotifyUserAsync({UserId}) failed", userId); }
    }
}
