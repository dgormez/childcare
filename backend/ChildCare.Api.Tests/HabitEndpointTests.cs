using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Api.Endpoints;
using ChildCare.Api.Models;
using Xunit;

namespace ChildCare.Api.Tests;

public class HabitEndpointTests(ChildCareWebAppFactory factory)
    : IClassFixture<ChildCareWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@test.com";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "password123" });
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private void ClearAuth() =>
        _client.DefaultRequestHeaders.Authorization = null;

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHabits_Unauthenticated_Returns401()
    {
        ClearAuth();
        var res = await _client.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetCompletions_Unauthenticated_Returns401()
    {
        ClearAuth();
        var res = await _client.GetAsync("/api/habits/completions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateHabit_ValidRequest_Returns201WithLocation()
    {
        Authorize(await RegisterAndGetTokenAsync());

        var res = await _client.PostAsJsonAsync("/api/habits",
            new { name = "Exercise", color = "#3b82f6", icon = "💪" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.NotNull(res.Headers.Location);

        var habit = await res.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);
        Assert.Equal("Exercise", habit.Name);
        Assert.Equal("#3b82f6", habit.Color);
        Assert.Equal("💪", habit.Icon);
        Assert.NotEqual(Guid.Empty, habit.Id);
    }

    [Fact]
    public async Task CreateHabit_DefaultsAppliedWhenColorAndIconOmitted()
    {
        Authorize(await RegisterAndGetTokenAsync());

        var res = await _client.PostAsJsonAsync("/api/habits",
            new { name = "Read" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var habit = await res.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);
        Assert.Equal("#3b82f6", habit.Color);
        Assert.Equal("✅", habit.Icon);
    }

    [Fact]
    public async Task GetHabits_AfterCreate_ReturnsCreatedHabit()
    {
        Authorize(await RegisterAndGetTokenAsync());

        await _client.PostAsJsonAsync("/api/habits", new { name = "Meditate" });

        var habits = await _client.GetFromJsonAsync<List<HabitDto>>("/api/habits");
        Assert.NotNull(habits);
        Assert.Single(habits);
        Assert.Equal("Meditate", habits[0].Name);
    }

    [Fact]
    public async Task UpdateHabit_ValidRequest_Returns200WithUpdatedFields()
    {
        Authorize(await RegisterAndGetTokenAsync());

        var createRes = await _client.PostAsJsonAsync("/api/habits",
            new { name = "Original", color = "#111111", icon = "🔥" });
        var created = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        var updateRes = await _client.PutAsJsonAsync($"/api/habits/{created!.Id}",
            new { name = "Updated", color = "#ffffff", icon = "⭐" });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("#ffffff", updated.Color);
        Assert.Equal("⭐", updated.Icon);
    }

    [Fact]
    public async Task DeleteHabit_ValidRequest_Returns204AndRemovedFromList()
    {
        Authorize(await RegisterAndGetTokenAsync());

        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "To Delete" });
        var created = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        var deleteRes = await _client.DeleteAsync($"/api/habits/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var habits = await _client.GetFromJsonAsync<List<HabitDto>>("/api/habits");
        Assert.Empty(habits!);
    }

    // ── Cross-user isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateHabit_AnotherUsersHabit_Returns404()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "User A habit" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        Authorize(await RegisterAndGetTokenAsync());
        var updateRes = await _client.PutAsJsonAsync($"/api/habits/{habit!.Id}",
            new { name = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);
    }

    [Fact]
    public async Task DeleteHabit_AnotherUsersHabit_Returns404()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "User A habit" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        Authorize(await RegisterAndGetTokenAsync());
        var deleteRes = await _client.DeleteAsync($"/api/habits/{habit!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, deleteRes.StatusCode);
    }

    // ── Completions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteHabit_ValidRequest_Returns200WithCompletion()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "Run" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        var res = await _client.PostAsync($"/api/habits/{habit!.Id}/complete", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var completion = await res.Content.ReadFromJsonAsync<CompletionDto>();
        Assert.NotNull(completion);
        Assert.Equal(habit.Id, completion.HabitId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), completion.Date);
    }

    [Fact]
    public async Task CompleteHabit_Idempotent_ReturnsSameCompletion()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "Stretch" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        var first  = await _client.PostAsync($"/api/habits/{habit!.Id}/complete", null);
        var second = await _client.PostAsync($"/api/habits/{habit.Id}/complete", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var c1 = await first.Content.ReadFromJsonAsync<CompletionDto>();
        var c2 = await second.Content.ReadFromJsonAsync<CompletionDto>();
        Assert.Equal(c1!.Id, c2!.Id);
    }

    [Fact]
    public async Task UncompleteHabit_ValidRequest_Returns204()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "Yoga" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        await _client.PostAsync($"/api/habits/{habit!.Id}/complete", null);
        var res = await _client.DeleteAsync($"/api/habits/{habit.Id}/complete");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetCompletions_AfterComplete_ReturnsCompletion()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "Walk" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();
        await _client.PostAsync($"/api/habits/{habit!.Id}/complete", null);

        var completions = await _client.GetFromJsonAsync<List<CompletionDto>>("/api/habits/completions");

        Assert.NotNull(completions);
        Assert.Single(completions);
        Assert.Equal(habit.Id, completions[0].HabitId);
    }

    [Fact]
    public async Task CompleteHabit_AnotherUsersHabit_Returns404()
    {
        Authorize(await RegisterAndGetTokenAsync());
        var createRes = await _client.PostAsJsonAsync("/api/habits", new { name = "User A habit" });
        var habit = await createRes.Content.ReadFromJsonAsync<HabitDto>();

        Authorize(await RegisterAndGetTokenAsync());
        var res = await _client.PostAsync($"/api/habits/{habit!.Id}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Free-tier limit ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateHabit_AtFreeTierLimit_Returns403()
    {
        Authorize(await RegisterAndGetTokenAsync());

        // Free tier allows exactly 3 habits
        for (var i = 1; i <= 3; i++)
            await _client.PostAsJsonAsync("/api/habits", new { name = $"Habit {i}" });

        var res = await _client.PostAsJsonAsync("/api/habits", new { name = "Habit 4" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("error"));
    }
}
