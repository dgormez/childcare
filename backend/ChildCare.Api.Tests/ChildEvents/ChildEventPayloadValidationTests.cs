using System.Net;
using System.Net.Http.Json;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 1 (T012): per-EventType payload validation — FR-002/FR-002a.</summary>
public class ChildEventPayloadValidationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, string DeviceToken, Guid ChildId)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Validation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        return (client, deviceToken, child.Id);
    }

    [Fact]
    public async Task RecordEvent_FieldOutsideSelectedType_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        // "ml" belongs to feeding_bottle, not diaper.
        var response = await PostChildEventAsync(client, deviceToken, childId, "diaper", DateTime.UtcNow, new { type = "wet", ml = 100 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.validation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RecordEvent_MissingRequiredField_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "feeding_bottle", DateTime.UtcNow, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_EmptyGrowthCheckPayload_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "growth_check", DateTime.UtcNow, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.empty_growth_check", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RecordEvent_GrowthCheckWithOnlyWeight_IsValid()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "growth_check", DateTime.UtcNow, new { weightKg = 12.5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── feature 009a: measurement -> growth_check is a hard cutover, no dual-write window ──

    [Fact]
    public async Task RecordEvent_LiteralMeasurementEventType_Returns400()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "measurement", DateTime.UtcNow, new { weightKg = 12.5 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.child_events.invalid_event_type", await response.Content.ReadAsStringAsync());
    }

    // ── feature 009a: `custom` event type (label + optional text) ──

    [Fact]
    public async Task RecordEvent_CustomMissingLabel_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_CustomWhitespaceLabel_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow, new { label = "   " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_CustomLabelOverMaxLength_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow, new { label = new string('a', 101) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.value_too_long", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RecordEvent_CustomLabelOnly_Returns201()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow, new { label = "Sunscreen applied" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_CustomLabelAndText_Returns201AndRoundTrips()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow,
            new { label = "Sunscreen applied", text = "Reapplied after outdoor play" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ChildCare.Contracts.Responses.ChildEventResponse>())!;
        Assert.Equal("custom", created.EventType);
    }

    [Fact]
    public async Task RecordEvent_CustomUnexpectedField_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        // "ml" belongs to feeding_bottle, not custom.
        var response = await PostChildEventAsync(client, deviceToken, childId, "custom", DateTime.UtcNow, new { label = "Sunscreen applied", ml = 100 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(45.0)]
    public async Task RecordEvent_TemperatureOutOfPhysiologicalRange_Returns422(decimal celsius)
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "temperature", DateTime.UtcNow, new { celsius });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_MedicationInvalidEnumValue_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "medication", DateTime.UtcNow,
            new { name = "aspirin", doseDescription = "5ml", reason = "fever" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_MedicationValidPayload_Returns201()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(client, deviceToken, childId, "medication", DateTime.UtcNow,
            new { name = "perdolan", doseDescription = "5ml", reason = "fever" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Convergence findings F1/F2: EndedAt/AdministeredBy restricted to their own event types ──

    [Fact]
    public async Task RecordEvent_EndedAtOnNonSleepEvent_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(
            client, deviceToken, childId, "diaper", DateTime.UtcNow, new { type = "wet" }, endedAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.ended_at_not_applicable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RecordEvent_AdministeredByOnNonMedicationOrTemperatureEvent_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var response = await PostChildEventAsync(
            client, deviceToken, childId, "note", DateTime.UtcNow, new { text = "hello" }, administeredByStaffId: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.administered_by_not_applicable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateEvent_SettingEndedAtOnNonSleepEvent_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var create = await PostChildEventAsync(client, deviceToken, childId, "diaper", DateTime.UtcNow, new { type = "wet" });
        var created = (await create.Content.ReadFromJsonAsync<ChildCare.Contracts.Responses.ChildEventResponse>())!;

        var response = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, endedAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.ended_at_not_applicable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateEvent_SettingAdministeredByOnNonMedicationOrTemperatureEvent_Returns422()
    {
        var (client, deviceToken, childId) = await SetupAsync();
        var create = await PostChildEventAsync(client, deviceToken, childId, "mood", DateTime.UtcNow, new { value = "good" });
        var created = (await create.Content.ReadFromJsonAsync<ChildCare.Contracts.Responses.ChildEventResponse>())!;

        var response = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, administeredByStaffId: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child_events.administered_by_not_applicable", await response.Content.ReadAsStringAsync());
    }
}
