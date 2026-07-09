using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>Shared HTTP helpers for feature 010's attendance test suite.</summary>
internal static class AttendanceTestSupport
{
    // ── Contracts (for planned_duration_minutes derivation, FR-006) ─────────────────────────

    public static List<ContractedDayRequest> Days(params DayOfWeek[] weekdays) =>
        weekdays.Select(w => new ContractedDayRequest(w, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    public static async Task<ContractResponse> CreateAndActivateContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var request = new CreateContractRequest(
            locationId, new DateOnly(2020, 1, 1), null,
            Days(weekdays.Length == 0 ? [DayOfWeek.Monday] : weekdays), 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(System.Net.HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── Attendance (device-authenticated) ────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> CheckInChildAsync(HttpClient client, string deviceToken, Guid childId, DateOnly date) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/attendance/check-in", deviceToken, new AttendanceCheckInRequest(childId, date)));

    public static Task<HttpResponseMessage> CheckOutChildAsync(HttpClient client, string deviceToken, Guid childId, DateOnly date) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/attendance/check-out", deviceToken, new AttendanceCheckOutRequest(childId, date)));

    public static Task<HttpResponseMessage> GetBkrAsync(HttpClient client, string deviceToken, Guid locationId) =>
        client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/attendance/bkr?locationId={locationId}", deviceToken));

    public static Task<HttpResponseMessage> MarkAbsentAsDeviceAsync(
        HttpClient client, string deviceToken, Guid childId, Guid locationId, DateOnly date, bool justified, string? reason = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/attendance/absence", deviceToken,
            new MarkAbsentRequest(childId, locationId, null, date, justified, reason)));

    public static Task<HttpResponseMessage> MarkAbsentAsDirectorAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly date, bool justified, string? reason = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/attendance/absence", accessToken,
            new MarkAbsentRequest(childId, locationId, null, date, justified, reason)));

    public static Task<HttpResponseMessage> PatchAttendanceAsDeviceAsync(
        HttpClient client, string deviceToken, Guid id, string? status = null, DateTime? checkInAt = null,
        DateTime? checkOutAt = null, bool? absenceJustified = null, string? absenceReason = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Patch, $"/api/attendance/{id}", deviceToken,
            new CorrectAttendanceRequest(status, checkInAt, checkOutAt, absenceJustified, absenceReason)));

    public static Task<HttpResponseMessage> PatchAttendanceAsDirectorAsync(
        HttpClient client, string accessToken, Guid id, string? status = null, DateTime? checkInAt = null,
        DateTime? checkOutAt = null, bool? absenceJustified = null, string? absenceReason = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/attendance/{id}", accessToken,
            new CorrectAttendanceRequest(status, checkInAt, checkOutAt, absenceJustified, absenceReason)));

    public static Task<HttpResponseMessage> DeleteAttendanceAsDeviceAsync(HttpClient client, string deviceToken, Guid id) =>
        client.SendAsync(DeviceRequest(HttpMethod.Delete, $"/api/attendance/{id}", deviceToken));

    public static Task<HttpResponseMessage> DeleteAttendanceAsDirectorAsync(HttpClient client, string accessToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/attendance/{id}", accessToken));

    public static Task<HttpResponseMessage> ListAttendanceAsync(
        HttpClient client, string accessToken, Guid locationId, DateOnly date, string? before = null, int? limit = null) =>
        client.SendAsync(AuthedRequest(
            HttpMethod.Get,
            $"/api/attendance?locationId={locationId}&date={date:yyyy-MM-dd}"
                + (before is null ? "" : $"&before={Uri.EscapeDataString(before)}") + (limit is null ? "" : $"&limit={limit}"),
            accessToken));
}
