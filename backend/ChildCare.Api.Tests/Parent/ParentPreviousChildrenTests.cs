using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Parent;

/// <summary>
/// Feature 030 User Story 5 — GET /api/parent/children/previous returns only the caller's
/// deactivated linked children, with enrollment-period dates (spec.md FR-015/FR-016,
/// research.md R8).
/// </summary>
public class ParentPreviousChildrenTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task GetPreviousChildren_DeactivatedLinkedChild_ReturnedWithEnrollmentPeriod()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PrevChildren Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var contractRequest = new CreateContractRequest(
            location.Id, new DateOnly(2026, 1, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var contract = (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, contractRequest))).Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        // A child can't be deactivated while an active contract exists (ContractChildDeactivationGuard) — terminate it first.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children/previous", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var previousChildren = (await response.Content.ReadFromJsonAsync<List<ParentPreviousChildResponse>>())!;
        var entry = Assert.Single(previousChildren);
        Assert.Equal(child.Id, entry.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), entry.EnrollmentStart);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), entry.EnrollmentEnd); // deactivated just now
    }

    [Fact]
    public async Task GetPreviousChildren_NoDeactivatedChildren_ReturnsEmptyList()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PrevChildren Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children/previous", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var previousChildren = (await response.Content.ReadFromJsonAsync<List<ParentPreviousChildResponse>>())!;
        Assert.Empty(previousChildren);
    }

    [Fact]
    public async Task GetPreviousChildren_ActiveChild_NeverAppearsHere()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PrevChildren Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (activeChild, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children/previous", parentToken));
        var previousChildren = (await response.Content.ReadFromJsonAsync<List<ParentPreviousChildResponse>>())!;

        Assert.DoesNotContain(previousChildren, c => c.Id == activeChild.Id);

        var activeResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children", parentToken));
        var activeChildren = (await activeResponse.Content.ReadFromJsonAsync<List<ParentChildResponse>>())!;
        Assert.Contains(activeChildren, c => c.Id == activeChild.Id);
    }
}
