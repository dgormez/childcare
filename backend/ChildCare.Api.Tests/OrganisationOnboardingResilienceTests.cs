using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

public class OrganisationOnboardingResilienceTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest(email)),
        };
        request.Headers.Add("X-Superadmin-Key", OrganisationOnboardingWebAppFactory.SuperAdminApiKey);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;
    }

    // ── User Story 3, scenario 1: partial failure is safely retryable (FR-014) ────

    [Fact]
    public async Task Register_AfterMidProvisioningFailure_CanBeRetriedToCompletion()
    {
        var client = factory.CreateClient();
        var invitation = await CreateInvitationAsync(client, "retry-me@example.com");

        var provisioningService = (TenantProvisioningService)factory.Services.GetRequiredService<ITenantProvisioningService>();
        provisioningService.FailureInjectionHookForTests = () => throw new InvalidOperationException("simulated mid-provisioning failure");

        var failedAttempt = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Retry Org", "Retry Director", invitation.Email, "password123"));
        Assert.Equal(HttpStatusCode.InternalServerError, failedAttempt.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var tenant = await db.Tenants.SingleAsync(t => t.Slug.StartsWith("retry-org"));
            Assert.NotEqual(ProvisioningStatus.Ready, tenant.ProvisioningStatus);
        }

        provisioningService.FailureInjectionHookForTests = null; // clear before retrying

        var retryAttempt = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Retry Org", "Retry Director", invitation.Email, "password123"));

        Assert.Equal(HttpStatusCode.Created, retryAttempt.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var tenantCount = await db.Tenants.CountAsync(t => t.Slug.StartsWith("retry-org"));
            Assert.Equal(1, tenantCount); // no duplicate Tenant row was created by the retry
        }
    }

    // ── User Story 3, scenario 2: concurrent attempts never create more than one org (FR-015) ─

    [Fact]
    public async Task Register_WithConcurrentAttempts_CreatesExactlyOneTenant()
    {
        var client = factory.CreateClient();
        var invitation = await CreateInvitationAsync(client, "concurrent@example.com");

        var requestFactory = () => client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Concurrent Org", "Concurrent Director", invitation.Email, "password123"));

        var responses = await Task.WhenAll(requestFactory(), requestFactory());

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, successCount); // exactly one attempt wins

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenants = await db.Tenants
            .Where(t => t.Slug.StartsWith("concurrent-org"))
            .ToListAsync();

        Assert.Single(tenants);
        Assert.Equal(ProvisioningStatus.Ready, tenants[0].ProvisioningStatus);
    }
}
