using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>Shared HTTP helpers for feature 013b's incident-reports test suite.</summary>
internal static class IncidentReportTestSupport
{
    /// <summary>FR-018's device-scoped GET requires an active ChildGroupAssignment — tests that
    /// exercise the device-token read path must assign the child to the paired device's group
    /// first (no test in this file needs it for filing itself, only for GET/PUT as device).</summary>
    public static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        Assert.Equal(System.Net.HttpStatusCode.Created, (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, DateOnly.FromDateTime(DateTime.UtcNow))))).StatusCode);

    public static Task<HttpResponseMessage> FileIncidentReportAsync(
        HttpClient client, string deviceToken, Guid childId, string description = "Scraped knee on the playground.",
        string injuryType = "scrape", DateTime? occurredAt = null, string? locationDetail = "outdoor",
        string? firstAidGiven = null, bool doctorCalled = false, string? doctorNotes = null,
        bool parentNotified = false, DateTime? parentNotifiedAt = null, string? parentNotifiedHow = null,
        string? witnesses = null, string? followUp = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/incident-reports/", deviceToken, new FileIncidentReportRequest(
            childId, occurredAt, locationDetail, description, injuryType, firstAidGiven, doctorCalled, doctorNotes,
            parentNotified, parentNotifiedAt, parentNotifiedHow, witnesses, followUp)));

    public static Task<HttpResponseMessage> GetIncidentReportAsDeviceAsync(HttpClient client, string deviceToken, Guid id) =>
        client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/incident-reports/{id}", deviceToken));

    public static Task<HttpResponseMessage> GetIncidentReportAsDirectorAsync(HttpClient client, string accessToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/incident-reports/{id}", accessToken));

    public static Task<HttpResponseMessage> ListIncidentReportsAsync(
        HttpClient client, string accessToken, Guid? childId = null, Guid? locationId = null,
        DateTime? from = null, DateTime? to = null, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (childId is not null) query.Add($"childId={childId}");
        if (locationId is not null) query.Add($"locationId={locationId}");
        if (from is not null) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to is not null) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        if (page is not null) query.Add($"page={page}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/incident-reports{qs}", accessToken));
    }

    public static Task<HttpResponseMessage> UpdateIncidentReportAsDeviceAsync(
        HttpClient client, string deviceToken, Guid id, UpdateIncidentReportRequest request) =>
        client.SendAsync(DeviceRequest(HttpMethod.Put, $"/api/incident-reports/{id}", deviceToken, request));

    public static Task<HttpResponseMessage> UpdateIncidentReportAsDirectorAsync(
        HttpClient client, string accessToken, Guid id, UpdateIncidentReportRequest request) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/incident-reports/{id}", accessToken, request));

    public static Task<HttpResponseMessage> GetIncidentReportPdfAsync(HttpClient client, string accessToken, Guid id, string? locale = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/incident-reports/{id}/pdf" + (locale is null ? "" : $"?locale={locale}"), accessToken));
}
