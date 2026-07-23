using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffDocuments;

/// <summary>
/// Feature 028/US3 (FR-011/FR-012/FR-012a/FR-013): HR document upload/list/delete, and the
/// director-only access boundary.
/// </summary>
public class StaffDossierTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginStaffAsync(HttpClient client, string orgSlug, string directorAccessToken)
    {
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Jane", "Doe", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"token=([^&\s]+)");
        var token = match.Groups[1].Value;

        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    private async Task<StaffDocumentResponse> UploadDocumentAsync(
        HttpClient client, string directorToken, Guid staffId, string documentType, DateOnly? validFrom, DateOnly? validUntil)
    {
        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staffId}/documents/upload-url", directorToken,
            new CreateStaffDocumentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadUrlBody = await uploadUrlResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var objectPath = uploadUrlBody.GetProperty("objectPath").GetString()!;

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staffId}/documents", directorToken,
            new CreateStaffDocumentRequest(documentType, "Test Document", objectPath, validFrom, validUntil)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        return (await createResponse.Content.ReadFromJsonAsync<StaffDocumentResponse>())!;
    }

    [Fact]
    public async Task UploadAndList_CreatedByResolvedFromJwt_WithWorkingDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Dossier Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var document = await UploadDocumentAsync(client, org.AccessToken, staff.Id, "employment_contract", null, null);
        Assert.NotNull(document.DownloadUrl);

        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var stored = await db.StaffDocuments.FirstAsync(d => d.Id == document.Id);
        Assert.NotEqual(Guid.Empty, stored.CreatedBy);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}/documents", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffDocumentResponse>>())!;
        Assert.Single(list, d => d.Id == document.Id);
    }

    [Fact]
    public async Task Delete_SoftDeletesRow_ExcludesFromList_AndDeletesGcsObject()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Dossier Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        var document = await UploadDocumentAsync(client, org.AccessToken, staff.Id, "qualification", null, null);

        var deleteResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staff.Id}/documents/{document.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}/documents", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffDocumentResponse>>())!;
        Assert.DoesNotContain(list, d => d.Id == document.Id);

        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var stored = await db.StaffDocuments.FirstAsync(d => d.Id == document.Id);
        Assert.NotNull(stored.DeletedAt);
        Assert.NotNull(stored.DeletedBy);

        var fakeStorage = factory.Services.GetRequiredService<FakeStaffDocumentStorage>();
        Assert.Contains(stored.ObjectPath, fakeStorage.DeletedPaths);
    }

    [Fact]
    public async Task NoDocumentsYet_ReturnsEmptyList_NotError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Dossier Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}/documents", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffDocumentResponse>>())!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task DossierEndpoints_RejectStaffAuthenticatedRequest()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Dossier Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken);
        var document = await UploadDocumentAsync(client, org.AccessToken, staff.Id, "qualification", null, null);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}/documents", staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staff.Id}/documents/upload-url", staffToken, new CreateStaffDocumentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.Forbidden, uploadUrlResponse.StatusCode);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/staff/{staff.Id}/documents", staffToken,
            new CreateStaffDocumentRequest("qualification", "x", "path", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var deleteResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staff.Id}/documents/{document.Id}", staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var contractsExpiringResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/contracts-expiring", staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, contractsExpiringResponse.StatusCode);
    }
}
