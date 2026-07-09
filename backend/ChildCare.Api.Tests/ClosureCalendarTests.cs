using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

public class ClosureCalendarTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 7, 13);
    private static readonly DateOnly Tuesday = new(2026, 7, 14);

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location, GroupResponse Group, string Schema)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Closure Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        return (client, org, location, group, schema);
    }

    private static async Task<ClosureDayResponse> CreateClosureAsync(
        HttpClient client, string accessToken, Guid locationId, DateOnly date, bool notify = true, string type = "holiday")
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", accessToken,
            new CreateClosureDayRequest(locationId, date, "Kerstvakantie", type, notify)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClosureDayResponse>())!;
    }

    private static Task<HttpResponseMessage> PublishClosureAsync(
        HttpClient client, string accessToken, Guid closureId, bool confirm = false) =>
        client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/closures/{closureId}/publish", accessToken, new PublishClosureDayRequest(confirm)));

    private static Task<HttpResponseMessage> CancelClosureAsync(HttpClient client, string accessToken, Guid closureId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closureId}/cancel", accessToken));

    private static async Task<HttpStatusCode> CallWithTokenAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var response = await client.SendAsync(AuthedRequest(method, url, token, body));
        return response.StatusCode;
    }

    private static string BuildTokenWithRole(Guid tenantId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, $"{role}_{Guid.NewGuid():N}@test.com"),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebAppFactoryBase.TestJwtSecret));
        var token = new JwtSecurityToken(
            issuer: TestWebAppFactoryBase.TestJwtIssuer,
            audience: TestWebAppFactoryBase.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<ChildResponse> CreateEnrolledChildWithParentAsync(
        HttpClient client, string accessToken, string schema, Guid locationId, string firstName = "Emma", string pushToken = "ExponentPushToken[test]")
    {
        var child = await CreateChildAsync(client, accessToken, firstName);
        await CreateAndActivateContractAsync(client, accessToken, child.Id, locationId, Monday.DayOfWeek, Tuesday.DayOfWeek);
        await CreatePickupEligibleContactWithPushTokenAsync(client, factory.Services, accessToken, child.Id, schema, pushToken);
        return child;
    }

    [Fact]
    public async Task CreateListUpdate_ByLocationYear_Works()
    {
        var (client, org, locationA, _, _) = await SetupAsync();
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");

        var closure = await CreateClosureAsync(client, org.AccessToken, locationA.Id, Monday);
        var update = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/closures/{closure.Id}", org.AccessToken,
            new UpdateClosureDayRequest("Studiedag", "training", false)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var listA = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/closures?locationId={locationA.Id}&year=2026", org.AccessToken));
        var itemsA = (await listA.Content.ReadFromJsonAsync<List<ClosureDayResponse>>())!;
        Assert.Single(itemsA);
        Assert.Equal("training", itemsA[0].ClosureType);
        Assert.False(itemsA[0].NotifyParents);

        var listB = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/closures?locationId={locationB.Id}&year=2026", org.AccessToken));
        var itemsB = (await listB.Content.ReadFromJsonAsync<List<ClosureDayResponse>>())!;
        Assert.Empty(itemsB);

        var missingLocation = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/closures?locationId={Guid.NewGuid()}&year=2026", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, missingLocation.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateLocationDate_ReturnsConflict()
    {
        var (client, org, location, _, _) = await SetupAsync();
        await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);

        var duplicate = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, Monday, "Duplicate", "holiday", true)));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Create_PastDate_ReturnsBadRequest()
    {
        var (client, org, location, _, _) = await SetupAsync();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, new DateOnly(2026, 7, 1), "Past", "holiday", true)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClosureEndpoints_RequireDirectorRole()
    {
        var (client, org, location, _, _) = await SetupAsync();
        var staffToken = BuildTokenWithRole(org.Organisation.Id, "staff");
        var parentToken = BuildTokenWithRole(org.Organisation.Id, "parent");

        foreach (var token in new[] { staffToken, parentToken })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, HttpMethod.Get, $"/api/closures?locationId={location.Id}&year=2026", token));
            Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(
                client, HttpMethod.Post, "/api/closures", token,
                new CreateClosureDayRequest(location.Id, Monday, "Nope", "holiday", true)));
        }
    }

    [Fact]
    public async Task Publish_NotifyEnabled_SendsPushAndCreatesParentMessage()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);

        var response = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PublishClosureDayResponse>())!;
        Assert.Equal(1, body.NotificationSummary.Recipients);
        Assert.Equal(1, body.NotificationSummary.MessagesCreated);
        Assert.Equal(1, body.NotificationSummary.PushSent);

        var push = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(push.Sent, p => p.PushToken == "ExponentPushToken[test]");

        var db = ResolveTenantDb(factory.Services, schema);
        Assert.Single(await db.ParentClosureMessages.Where(m => m.ClosureDayId == closure.Id).ToListAsync());
    }

    [Fact]
    public async Task Publish_NotifyDisabled_CreatesNoParentNotification()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday, notify: false);

        var response = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PublishClosureDayResponse>())!;
        Assert.Equal(0, body.NotificationSummary.Recipients);

        var db = ResolveTenantDb(factory.Services, schema);
        Assert.Empty(await db.ParentClosureMessages.Where(m => m.ClosureDayId == closure.Id).ToListAsync());
    }

    [Fact]
    public async Task Publish_DeduplicatesParentLinkedToMultipleChildren()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");
        await CreateAndActivateContractAsync(client, org.AccessToken, childA.Id, location.Id, Monday.DayOfWeek);
        await CreateAndActivateContractAsync(client, org.AccessToken, childB.Id, location.Id, Monday.DayOfWeek);
        var contact = await CreateContactAsync(client, org.AccessToken);
        foreach (var child in new[] { childA, childB })
        {
            var link = await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
                new LinkContactToChildRequest(contact.Id, "Mother", true, false)));
            Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        }
        var db = ResolveTenantDb(factory.Services, schema);
        (await db.Contacts.SingleAsync(c => c.Id == contact.Id)).PushToken = "ExponentPushToken[shared]";
        await db.SaveChangesAsync();
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);

        var response = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PublishClosureDayResponse>())!;
        Assert.Equal(1, body.NotificationSummary.Recipients);
        Assert.Single(await db.ParentClosureMessages.Where(m => m.ClosureDayId == closure.Id).ToListAsync());
    }

    [Fact]
    public async Task Publish_PushFailure_RecordsFailedDeliveryButSucceeds()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);
        factory.Services.GetRequiredService<FakeExpoPushSender>().ThrowOnSend = true;

        var response = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PublishClosureDayResponse>())!;
        Assert.Equal(1, body.NotificationSummary.PushFailed);

        factory.Services.GetRequiredService<FakeExpoPushSender>().ThrowOnSend = false;
    }

    [Fact]
    public async Task Publish_CreatesClosureAttendanceRecords_AndCheckInRejects()
    {
        var (client, org, location, group, schema) = await SetupAsync();
        var child = await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday, notify: false);

        var publish = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var list = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday);
        var page = (await list.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.Single(page.Items);
        Assert.Equal("closure", page.Items[0].Status);

        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Forbidden, checkIn.StatusCode);
    }

    [Fact]
    public async Task Publish_CheckedInChildRequiresConfirmation_ThenPreservesPriorState()
    {
        var (client, org, location, group, schema) = await SetupAsync();
        var child = await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Created, checkIn.StatusCode);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday, notify: false);

        var blocked = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var confirmed = await PublishClosureAsync(client, org.AccessToken, closure.Id, confirm: true);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        var db = ResolveTenantDb(factory.Services, schema);
        var attendance = await db.AttendanceRecords.SingleAsync(r => r.ChildId == child.Id && r.LocationId == location.Id && r.Date == Monday);
        Assert.Equal(AttendanceStatus.Closure, attendance.Status);
        Assert.NotNull(attendance.PriorStateJson);
        Assert.Equal(closure.Id, attendance.ClosureDayId);

        var closureRow = await db.KdvClosureDays.SingleAsync(c => c.Id == closure.Id);
        Assert.NotNull(closureRow.AttendanceGeneratedAt);
        Assert.Equal(closureRow.PublishedBy, closureRow.AttendanceGeneratedBy);
    }

    [Fact]
    public async Task BillableExclusions_ReturnsPublishedNonCancelledDatesOnly()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var published = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday, notify: false);
        var draft = await CreateClosureAsync(client, org.AccessToken, location.Id, Tuesday, notify: false);
        var cancelled = await CreateClosureAsync(client, org.AccessToken, location.Id, new DateOnly(2026, 7, 15), notify: false);
        Assert.Equal(HttpStatusCode.OK, (await PublishClosureAsync(client, org.AccessToken, published.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PublishClosureAsync(client, org.AccessToken, cancelled.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CancelClosureAsync(client, org.AccessToken, cancelled.Id)).StatusCode);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Get,
            $"/api/closures/billable-exclusions?locationId={location.Id}&from=2026-07-01&to=2026-07-31",
            org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BillableClosureDatesResponse>())!;
        Assert.Contains(Monday, body.Dates);
        Assert.DoesNotContain(Tuesday, body.Dates);
        Assert.DoesNotContain(new DateOnly(2026, 7, 15), body.Dates);
        Assert.NotEqual(Guid.Empty, draft.Id);
    }

    [Fact]
    public async Task Cancel_PublishedClosure_SendsCancellationAndReleasesAttendance()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);
        Assert.Equal(HttpStatusCode.OK, (await PublishClosureAsync(client, org.AccessToken, closure.Id)).StatusCode);

        var cancel = await CancelClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var body = (await cancel.Content.ReadFromJsonAsync<CancelClosureDayResponse>())!;
        Assert.Equal(1, body.AttendanceRecordsReleased);
        Assert.Equal(1, body.NotificationSummary.MessagesCreated);

        var db = ResolveTenantDb(factory.Services, schema);
        Assert.Empty(await db.AttendanceRecords.Where(r => r.ClosureDayId == closure.Id).ToListAsync());
        Assert.Equal(2, await db.ParentClosureMessages.CountAsync(m => m.ClosureDayId == closure.Id));
    }

    [Fact]
    public async Task Cancel_Draft_RemovesWithoutNotification()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday);

        var cancel = await CancelClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var db = ResolveTenantDb(factory.Services, schema);
        Assert.False(await db.KdvClosureDays.AnyAsync(c => c.Id == closure.Id));
        Assert.Empty(await db.ParentClosureMessages.Where(m => m.ClosureDayId == closure.Id).ToListAsync());
    }

    [Fact]
    public async Task Cancel_PreservesManuallyChangedAttendanceRecords()
    {
        var (client, org, location, _, schema) = await SetupAsync();
        var child = await CreateEnrolledChildWithParentAsync(client, org.AccessToken, schema, location.Id);
        var closure = await CreateClosureAsync(client, org.AccessToken, location.Id, Monday, notify: false);
        Assert.Equal(HttpStatusCode.OK, (await PublishClosureAsync(client, org.AccessToken, closure.Id)).StatusCode);

        var db = ResolveTenantDb(factory.Services, schema);
        var attendance = await db.AttendanceRecords.SingleAsync(r => r.ChildId == child.Id && r.LocationId == location.Id && r.Date == Monday);
        attendance.Status = AttendanceStatus.Present;
        attendance.CheckInAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var cancel = await CancelClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var body = (await cancel.Content.ReadFromJsonAsync<CancelClosureDayResponse>())!;
        Assert.Equal(0, body.AttendanceRecordsReleased);
        Assert.Equal(1, body.AttendanceRecordsPreserved);

        var preserved = await db.AttendanceRecords.SingleAsync(r => r.Id == attendance.Id);
        Assert.Equal(AttendanceStatus.Present, preserved.Status);
    }
}
