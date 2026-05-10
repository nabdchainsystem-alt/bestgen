using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Idempotency;

/// <summary>
/// Apply to write actions that must not run twice for the same client request.
/// The client sends an <c>Idempotency-Key</c> header; we record (key, route)
/// before the action runs. A second request with the same key returns HTTP 409
/// instead of re-running the action.
///
/// The header is optional — actions still work without it. Adding the header
/// is the client's opt-in to retry safety.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public int WindowMinutes { get; set; } = 30;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }
        if (key.Length > 120)
        {
            context.Result = new BadRequestObjectResult(new { error = "Idempotency-Key too long (max 120)." });
            return;
        }

        var routeKey = $"{http.Request.Method}:{http.Request.Path.Value}";
        var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();

        // Reject obvious replays before the action runs.
        var existing = await db.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k =>
                k.Key == key && k.RouteKey == routeKey && k.ExpiresAt > DateTime.UtcNow);
        if (existing is not null)
        {
            context.Result = new ObjectResult(new
            {
                error = "Duplicate request — already processed.",
                processedAt = existing.CreatedAt,
                originalStatus = existing.ResponseStatus
            })
            { StatusCode = StatusCodes.Status409Conflict };
            return;
        }

        // Reserve the key. Race-safe via unique index — concurrent inserts trip a DbUpdateException.
        db.IdempotencyKeys.Add(new IdempotencyKey
        {
            Key = key,
            RouteKey = routeKey,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(WindowMinutes)
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            context.Result = new ObjectResult(new { error = "Duplicate request — concurrent retry detected." })
            { StatusCode = StatusCodes.Status409Conflict };
            return;
        }

        var executed = await next();

        // Record the outcome on the reserved key (best-effort; never throws).
        try
        {
            var entity = await db.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key && k.RouteKey == routeKey);
            if (entity is not null)
            {
                if (executed.Exception is not null)
                {
                    // The action threw — drop the key so the client can retry.
                    db.IdempotencyKeys.Remove(entity);
                }
                else
                {
                    entity.ResponseStatus = http.Response.StatusCode;
                }
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // swallow — idempotency bookkeeping never breaks the user response.
        }
    }
}
