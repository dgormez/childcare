using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffDocuments;

/// <summary>Feature 028/US3 (FR-014): the 60-day contract-expiry boundary, inclusive of
/// already-past dates, restricted to employment_contract documents only.</summary>
public class ContractsExpiringTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<StaffDocumentResponse> UploadDocumentAsync(
        HttpClient client, string directorToken, Guid staffId, string documentType, DateOnly? validUntil)
    {
        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staffId}/documents/upload-url", directorToken,
            new CreateStaffDocumentUploadUrlRequest("application/pdf")));
        var uploadUrlBody = await uploadUrlResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var objectPath = uploadUrlBody.GetProperty("objectPath").GetString()!;

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staffId}/documents", directorToken,
            new CreateStaffDocumentRequest(documentType, "Test Document", objectPath, null, validUntil)));
        return (await createResponse.Content.ReadFromJsonAsync<StaffDocumentResponse>())!;
    }

    [Fact]
    public async Task ContractsExpiring_IncludesWithin60Days_ExcludesFurtherOut_IncludesAlreadyPast()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Expiry Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var withinWindow = await CreateStaffAsync(client, org.AccessToken, "Within");
        await UploadDocumentAsync(client, org.AccessToken, withinWindow.Id, "employment_contract", today.AddDays(30));

        var furtherOut = await CreateStaffAsync(client, org.AccessToken, "FurtherOut");
        await UploadDocumentAsync(client, org.AccessToken, furtherOut.Id, "employment_contract", today.AddDays(90));

        var alreadyPast = await CreateStaffAsync(client, org.AccessToken, "AlreadyPast");
        await UploadDocumentAsync(client, org.AccessToken, alreadyPast.Id, "employment_contract", today.AddDays(-10));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/contracts-expiring", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<List<ContractExpiringResponse>>())!;

        Assert.Contains(list, e => e.StaffProfileId == withinWindow.Id);
        Assert.DoesNotContain(list, e => e.StaffProfileId == furtherOut.Id);
        Assert.Contains(list, e => e.StaffProfileId == alreadyPast.Id && e.IsExpired);
    }

    [Fact]
    public async Task ContractsExpiring_ExcludesNonContractDocumentTypes_RegardlessOfDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Expiry Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var staff = await CreateStaffAsync(client, org.AccessToken);
        await UploadDocumentAsync(client, org.AccessToken, staff.Id, "training", today.AddDays(5));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/contracts-expiring", org.AccessToken));
        var list = (await response.Content.ReadFromJsonAsync<List<ContractExpiringResponse>>())!;

        Assert.DoesNotContain(list, e => e.StaffProfileId == staff.Id);
    }
}
