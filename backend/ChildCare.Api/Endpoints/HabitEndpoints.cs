using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ChildCare.Api.Data;
using ChildCare.Api.Models;

namespace ChildCare.Api.Endpoints;

public static class HabitEndpoints
{
    private const int FreeHabitLimit = 3;

    public static void MapHabitEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/habits")
                       .WithTags("Habits")
                       .RequireAuthorization()
                       .RequireRateLimiting("api-user");

        // ── GET /api/habits ──────────────────────────────────────────────────
        group.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = GetUserId(ctx);
            var habits = await db.Habits
                .Where(h => h.UserId == userId)
                .OrderBy(h => h.CreatedAt)
                .Select(h => new HabitDto(h.Id, h.UserId, h.Name, h.Color, h.Icon, h.CreatedAt))
                .ToListAsync();

            return Results.Ok(habits);
        });

        // ── GET /api/habits/completions?from=&to= ────────────────────────────
        group.MapGet("/completions", async (HttpContext ctx, AppDbContext db, DateOnly? from, DateOnly? to) =>
        {
            var userId  = GetUserId(ctx);
            var dateFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var dateTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var completions = await db.HabitCompletions
                .Where(c => c.UserId == userId && c.Date >= dateFrom && c.Date <= dateTo)
                .Select(c => new CompletionDto(c.Id, c.HabitId, c.UserId, c.Date, c.CreatedAt))
                .ToListAsync();

            return Results.Ok(completions);
        });

        // ── POST /api/habits ─────────────────────────────────────────────────
        group.MapPost("/", async (HttpContext ctx, CreateHabitRequest req, AppDbContext db) =>
        {
            var userId = GetUserId(ctx);
            var user   = await db.Users.FindAsync(userId);
            if (user is null) return Results.Unauthorized();

            // Free tier: max 3 habits unless Active or Trialing
            var isSubscribed = user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing;
            if (!isSubscribed)
            {
                var count = await db.Habits.CountAsync(h => h.UserId == userId);
                if (count >= FreeHabitLimit)
                    return Results.Json(new { error = "upgrade_required", limit = FreeHabitLimit },
                        statusCode: 403);
            }

            var habit = new Habit
            {
                UserId = userId,
                Name   = req.Name.Trim(),
                Color  = req.Color ?? "#3b82f6",
                Icon   = req.Icon  ?? "✅",
            };

            db.Habits.Add(habit);
            await db.SaveChangesAsync();
            return Results.Created($"/api/habits/{habit.Id}",
                new HabitDto(habit.Id, habit.UserId, habit.Name, habit.Color, habit.Icon, habit.CreatedAt));
        });

        // ── PUT /api/habits/{id} ─────────────────────────────────────────────
        group.MapPut("/{id:guid}", async (HttpContext ctx, Guid id, UpdateHabitRequest req, AppDbContext db) =>
        {
            var userId = GetUserId(ctx);
            var habit  = await db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
            if (habit is null) return Results.NotFound();

            habit.Name  = req.Name.Trim();
            habit.Color = req.Color ?? habit.Color;
            habit.Icon  = req.Icon  ?? habit.Icon;
            await db.SaveChangesAsync();

            return Results.Ok(new HabitDto(habit.Id, habit.UserId, habit.Name, habit.Color, habit.Icon, habit.CreatedAt));
        });

        // ── DELETE /api/habits/{id} ──────────────────────────────────────────
        group.MapDelete("/{id:guid}", async (HttpContext ctx, Guid id, AppDbContext db) =>
        {
            var userId = GetUserId(ctx);
            var habit  = await db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
            if (habit is null) return Results.NotFound();

            db.Habits.Remove(habit);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── POST /api/habits/{id}/complete ───────────────────────────────────
        group.MapPost("/{id:guid}/complete", async (HttpContext ctx, Guid id, AppDbContext db, DateOnly? date) =>
        {
            var userId  = GetUserId(ctx);
            var today   = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var habitExists = await db.Habits.AnyAsync(h => h.Id == id && h.UserId == userId);
            if (!habitExists) return Results.NotFound();

            var existing = await db.HabitCompletions
                .FirstOrDefaultAsync(c => c.HabitId == id && c.Date == today);
            if (existing is not null)
                return Results.Ok(new CompletionDto(existing.Id, existing.HabitId, existing.UserId, existing.Date, existing.CreatedAt));

            var completion = new HabitCompletion
            {
                HabitId = id,
                UserId  = userId,
                Date    = today,
            };
            db.HabitCompletions.Add(completion);
            await db.SaveChangesAsync();
            return Results.Ok(new CompletionDto(completion.Id, completion.HabitId, completion.UserId, completion.Date, completion.CreatedAt));
        });

        // ── DELETE /api/habits/{id}/complete ─────────────────────────────────
        group.MapDelete("/{id:guid}/complete", async (HttpContext ctx, Guid id, AppDbContext db, DateOnly? date) =>
        {
            var userId = GetUserId(ctx);
            var today  = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var completion = await db.HabitCompletions
                .FirstOrDefaultAsync(c => c.HabitId == id && c.UserId == userId && c.Date == today);

            if (completion is null) return Results.NoContent();

            db.HabitCompletions.Remove(completion);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static Guid GetUserId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record HabitDto(Guid Id, Guid UserId, string Name, string Color, string Icon, DateTime CreatedAt);

public record CompletionDto(Guid Id, Guid HabitId, Guid UserId, DateOnly Date, DateTime CreatedAt);

public record CreateHabitRequest(
    [Required, MaxLength(100)] string Name,
    string? Color,
    string? Icon);

public record UpdateHabitRequest(
    [Required, MaxLength(100)] string Name,
    string? Color,
    string? Icon);
