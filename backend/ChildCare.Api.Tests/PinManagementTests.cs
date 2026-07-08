using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>User Story 2 (feature 008a): a director sets/resets a caregiver's check-in PIN via
/// the API (no UI in this feature — spec Assumptions). Covers T027-T029.</summary>
public class PinManagementTests(OrganisationOnboardingWebAppFactory factory)
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

    /// <summary>Creates a staff profile, accepts its invitation, and logs in — a caregiver with
    /// a real account password, distinct from the check-in PIN this suite provisions.</summary>
    private async Task<(StaffResponse Staff, string Email)> CreateOnboardedStaffAsync(HttpClient client, RegisterOrganisationResponse org)
    {
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Jane", "Doe", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var token = ExtractLatestStaffInviteToken(factory, email);
        var acceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        return (staff, email);
    }

    // ── T027: PUT stores a bcrypt hash, never the plaintext PIN; account password unaffected ──

    [Fact]
    public async Task SetPin_StoresBcryptHash_NeverPlaintext_AccountPasswordUnaffected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PinHash Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, email) = await CreateOnboardedStaffAsync(client, org);
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var setResponse = await SetPinRawAsync(client, org.AccessToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.NoContent, setResponse.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var profile = await db.StaffProfiles.SingleAsync(p => p.Id == staff.Id);
        Assert.NotNull(profile.PinHash);
        Assert.NotEqual("1234", profile.PinHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("1234", profile.PinHash));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // ── T028: duplicate PIN at same location rejected; different, non-overlapping location succeeds ──

    [Fact]
    public async Task SetPin_DuplicateAtSameLocation_Rejected_DifferentLocationSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PinUnique Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");

        var staffA = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, locationA.Id, "1234", "Alice");
        var staffB = await CreateStaffAsync(client, org.AccessToken, "Bob");
        await AssignEligibilityAsync(client, org.AccessToken, staffB.Id, locationA.Id);

        var duplicateResponse = await SetPinRawAsync(client, org.AccessToken, staffB.Id, "1234");
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Contains("errors.pin.not_unique_at_location", await duplicateResponse.Content.ReadAsStringAsync());

        var staffC = await CreateStaffAsync(client, org.AccessToken, "Carol");
        await AssignEligibilityAsync(client, org.AccessToken, staffC.Id, locationB.Id);
        var differentLocationResponse = await SetPinRawAsync(client, org.AccessToken, staffC.Id, "1234");
        Assert.Equal(HttpStatusCode.NoContent, differentLocationResponse.StatusCode);
    }

    // ── T029: DELETE clears the hash; caregiver can no longer check in until a new PIN is set ──

    [Fact]
    public async Task DeletePin_ClearsHash_CaregiverCannotCheckInUntilNewPinSet()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PinDelete Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var deleteResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staff.Id}/pin", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var profile = await db.StaffProfiles.SingleAsync(p => p.Id == staff.Id);
        Assert.Null(profile.PinHash);

        var checkInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = staff.Id, pin = "1234" }),
        };
        checkInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var checkInResponse = await client.SendAsync(checkInRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, checkInResponse.StatusCode);
        Assert.Contains("errors.pin.invalid", await checkInResponse.Content.ReadAsStringAsync());

        var newPinResponse = await SetPinRawAsync(client, org.AccessToken, staff.Id, "5678");
        Assert.Equal(HttpStatusCode.NoContent, newPinResponse.StatusCode);

        var secondCheckInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = staff.Id, pin = "5678" }),
        };
        secondCheckInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var secondCheckInResponse = await client.SendAsync(secondCheckInRequest);
        Assert.Equal(HttpStatusCode.OK, secondCheckInResponse.StatusCode);
    }
}
