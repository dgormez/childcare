using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 014 — spec.md User Story 1 (director generates a month's invoices for a location).
/// FR-001/FR-002/FR-003/FR-014. Attendance fixtures are seeded directly via ITenantDbContext
/// (AttendanceCorrectionTests precedent) rather than through the full device check-in ceremony,
/// since these tests exercise invoicing's consumption of attendance data, not attendance
/// recording itself.
/// </summary>
public class GenerateInvoicesTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<ContractResponse> CreateContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly startDate, DateOnly? endDate,
        int dailyRateCents = 3500, DayOfWeek weekday = DayOfWeek.Monday)
    {
        var request = new CreateContractRequest(
            locationId, startDate, endDate,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))],
            dailyRateCents, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    private static void SeedAttendance(
        Application.Common.ITenantDbContext db, Guid childId, Guid locationId, DateOnly date,
        AttendanceStatus status, bool? absenceJustified = null, int? plannedDurationMinutes = null)
    {
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            ChildId = childId,
            LocationId = locationId,
            Date = date,
            Status = status,
            AbsenceJustified = absenceJustified,
            PlannedDurationMinutes = plannedDurationMinutes,
        });
    }

    private static async Task<List<InvoiceResponse>> GenerateAsync(HttpClient client, string accessToken, Guid locationId, int year, int month)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
    }

    [Fact]
    public async Task Generate_ComputesBillableDaysCorrectly_PresentPlusUnjustifiedMinusClosure()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 1), null);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 2), AttendanceStatus.Present, plannedDurationMinutes: 480);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 3), AttendanceStatus.Present, plannedDurationMinutes: 480);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 4), AttendanceStatus.Absent, absenceJustified: true);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 5), AttendanceStatus.Absent, absenceJustified: false);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 6), AttendanceStatus.Closure);
        await db.SaveChangesAsync();

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var invoice = Assert.Single(invoices);
        Assert.Equal(contract.Id, invoice.ContractId);
        Assert.Equal("draft", invoice.Status);
        Assert.Equal(2, invoice.LineItems.PresentDays);
        Assert.Equal(1, invoice.LineItems.UnjustifiedAbsentDays);
        Assert.Equal(1, invoice.LineItems.ClosureDaysExcluded);
        Assert.Equal(3500, invoice.LineItems.DailyRateCents);
        // (2 present + 1 unjustified) * 3500 = 10500. Justified absence and closure never billed.
        Assert.Equal(10500, invoice.SubtotalCents);
        Assert.Equal(10500, invoice.TotalCents);
    }

    [Fact]
    public async Task Generate_MidMonthContractStart_OnlyCountsDaysFromStartOnward()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        // Contract starts mid-month (Aug 15) — attendance before that date must not count.
        await CreateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 15), null);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 10), AttendanceStatus.Present); // before start — must not count
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 20), AttendanceStatus.Present); // after start — counts
        await db.SaveChangesAsync();

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var invoice = Assert.Single(invoices);
        Assert.Equal(1, invoice.LineItems.PresentDays);
    }

    [Fact]
    public async Task Generate_MidMonthContractEnd_OnlyCountsDaysThroughEndDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 1), new DateOnly(2027, 8, 15));

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 10), AttendanceStatus.Present); // before end — counts
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 20), AttendanceStatus.Present); // after end — must not count
        await db.SaveChangesAsync();

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var invoice = Assert.Single(invoices);
        Assert.Equal(1, invoice.LineItems.PresentDays);
    }

    [Fact]
    public async Task Generate_ChildAtTwoLocations_ProducesTwoIndependentInvoices()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var firstLocation = await CreateLocationAsync(client, org.AccessToken, "First");
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateContractAsync(client, org.AccessToken, child.Id, firstLocation.Id, new DateOnly(2027, 8, 1), null, dailyRateCents: 3000, weekday: DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child.Id, secondLocation.Id, new DateOnly(2027, 8, 1), null, dailyRateCents: 4000, weekday: DayOfWeek.Tuesday);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, firstLocation.Id, new DateOnly(2027, 8, 2), AttendanceStatus.Present);
        SeedAttendance(db, child.Id, secondLocation.Id, new DateOnly(2027, 8, 3), AttendanceStatus.Present);
        SeedAttendance(db, child.Id, secondLocation.Id, new DateOnly(2027, 8, 4), AttendanceStatus.Present);
        await db.SaveChangesAsync();

        var firstInvoices = await GenerateAsync(client, org.AccessToken, firstLocation.Id, 2027, 8);
        var secondInvoices = await GenerateAsync(client, org.AccessToken, secondLocation.Id, 2027, 8);

        var firstInvoice = Assert.Single(firstInvoices);
        var secondInvoice = Assert.Single(secondInvoices);
        Assert.Equal(1, firstInvoice.LineItems.PresentDays);
        Assert.Equal(3000, firstInvoice.SubtotalCents);
        Assert.Equal(2, secondInvoice.LineItems.PresentDays);
        Assert.Equal(8000, secondInvoice.SubtotalCents);
        Assert.NotEqual(firstInvoice.Id, secondInvoice.Id);
        Assert.NotEqual(firstInvoice.OgmReference, secondInvoice.OgmReference);
    }

    [Fact]
    public async Task Generate_CalledTwiceForSameLocationMonth_DoesNotDuplicateInvoices()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 1), null);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 2), AttendanceStatus.Present);
        await db.SaveChangesAsync();

        var first = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);
        var second = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(first[0].Id, second[0].Id);
        Assert.Equal(first[0].OgmReference, second[0].OgmReference);
    }

    [Fact]
    public async Task Generate_ContractNeverActivated_ProducesNoInvoice()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);

        // Create but never activate — stays Draft, which per spec.md's Edge Cases must never
        // yield an invoice even though its dates would otherwise overlap the month.
        var request = new CreateContractRequest(
            location.Id, new DateOnly(2027, 8, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, request));

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.Empty(invoices);
    }

    [Fact]
    public async Task Generate_ZeroBillableDays_StillGeneratesInvoiceWithZeroTotal()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 1), null);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 2), AttendanceStatus.Absent, absenceJustified: true);
        SeedAttendance(db, child.Id, location.Id, new DateOnly(2027, 8, 3), AttendanceStatus.Closure);
        await db.SaveChangesAsync();

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var invoice = Assert.Single(invoices);
        Assert.Equal(0, invoice.SubtotalCents);
        Assert.Equal(0, invoice.TotalCents);
        Assert.NotNull(invoice.OgmReference);
    }
}
