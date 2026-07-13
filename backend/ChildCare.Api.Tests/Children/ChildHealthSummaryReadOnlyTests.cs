using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>User Story 4 (spec.md FR-014): a caregiver/device credential can never write a
/// vaccine or health record — only DirectorOnly routes exist for creating them.</summary>
public class ChildHealthSummaryReadOnlyTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static string ExtractLatestStaffInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    private static async Task<string> CreateAndLoginCaregiverAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string orgSlug, string directorAccessToken, params Guid[] locationIds)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<Contracts.Responses.StaffResponse>())!;

        foreach (var locationId in locationIds)
            await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationId}", directorAccessToken));

        var token = ExtractLatestStaffInviteToken(factory, email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<Contracts.Responses.AuthSessionResponse>())!;
        return session.AccessToken;
    }

    [Fact]
    public async Task Caregiver_CannotCreateVaccineOrHealthRecord()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ReadOnly Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken);
        var caregiverToken = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var vaccineResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", caregiverToken,
            new CreateVaccineRecordRequest("DTP", null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, vaccineResponse.StatusCode);

        var healthResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", caregiverToken,
            new CreateHealthRecordRequest("allergy", "Title", "Description", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, healthResponse.StatusCode);
    }

    [Fact]
    public async Task DeviceToken_CannotCreateVaccineOrHealthRecord()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ReadOnly Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var vaccineResponse = await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", deviceToken,
            new CreateVaccineRecordRequest("DTP", null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, vaccineResponse.StatusCode);

        var healthResponse = await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", deviceToken,
            new CreateHealthRecordRequest("allergy", "Title", "Description", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, healthResponse.StatusCode);
    }
}
