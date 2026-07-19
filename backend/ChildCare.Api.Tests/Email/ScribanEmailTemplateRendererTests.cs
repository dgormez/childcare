using ChildCare.Infrastructure.Email;
using Xunit;

namespace ChildCare.Api.Tests.Email;

public class ScribanEmailTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_BulkEmail_WrapsContentInSharedLayout()
    {
        var renderer = new ScribanEmailTemplateRenderer();

        var html = await renderer.RenderAsync(
            "bulk-email",
            "nl",
            new { SubjectHtml = "Test onderwerp", BodyHtml = "Hallo &amp; welkom" });

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("lang=\"nl\"", html);
        Assert.Contains("Test onderwerp", html);
        Assert.Contains("Hallo &amp; welkom", html);
    }

    [Fact]
    public async Task RenderAsync_DailyReport_NoEvents_ShowsNoUpdatesBranch()
    {
        var renderer = new ScribanEmailTemplateRenderer();

        var html = await renderer.RenderAsync(
            "daily-report",
            "en",
            new
            {
                Title = "Emma's day",
                HasEvents = false,
                NoUpdatesText = "No updates logged today.",
                NapsLabel = "Naps",
                NapsCount = 0,
                BottlesLabel = "Bottles",
                BottlesCount = 0,
                DiapersLabel = "Diapers",
                DiapersCount = 0,
                MoodLabel = (string?)null,
                MoodText = (string?)null,
                TemperatureLabel = (string?)null,
                TemperatureText = (string?)null,
                MedicationText = (string?)null,
                ActivitiesLabel = "Activities",
                Activities = Array.Empty<string>(),
                GroupActivitiesLabel = "Group activities",
                GroupActivities = Array.Empty<object>(),
                UnsubscribeText = "Unsubscribe",
                UnsubscribeUrl = "https://example.com/unsubscribe?token=abc&org=acme",
            });

        Assert.Contains("No updates logged today.", html);
        Assert.DoesNotContain("Naps", html);
    }

    [Fact]
    public async Task RenderAsync_DailyReport_WithEvents_ShowsCounts()
    {
        var renderer = new ScribanEmailTemplateRenderer();

        var html = await renderer.RenderAsync(
            "daily-report",
            "en",
            new
            {
                Title = "Emma's day",
                HasEvents = true,
                NoUpdatesText = "No updates logged today.",
                NapsLabel = "Naps",
                NapsCount = 2,
                BottlesLabel = "Bottles",
                BottlesCount = 3,
                DiapersLabel = "Diapers",
                DiapersCount = 4,
                MoodLabel = "Mood",
                MoodText = "Happy",
                TemperatureLabel = (string?)null,
                TemperatureText = (string?)null,
                MedicationText = (string?)null,
                ActivitiesLabel = "Activities",
                Activities = new[] { "Painted a picture" },
                GroupActivitiesLabel = "Group activities",
                GroupActivities = Array.Empty<object>(),
                UnsubscribeText = "Unsubscribe",
                UnsubscribeUrl = "https://example.com/unsubscribe?token=abc&org=acme",
            });

        Assert.Contains("Naps", html);
        Assert.Contains(">2<", html);
        Assert.Contains("Happy", html);
        Assert.Contains("Painted a picture", html);
        Assert.DoesNotContain("No updates logged today.", html);
    }
}
