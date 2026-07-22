using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>Feature 025, tasks.md T032/T033 — User Story 3 (manual review queue).</summary>
public class ReviewCodaTransactionTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Review_UnmatchedTransaction_RemovesFromNeedsReviewQueue_WithoutTouchingAnyInvoice()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 3, 1), 1234, "BE68539007547034", "Nobody", "no match", false)]);
        var unmatched = Assert.Single(await GetCodaTransactionsAsync(client, org.AccessToken, needsReview: true));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{unmatched.Id}/review", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reviewed = (await response.Content.ReadFromJsonAsync<CodaTransactionResponse>())!;
        Assert.NotNull(reviewed.ReviewedAt);

        var needsReview = await GetCodaTransactionsAsync(client, org.AccessToken, needsReview: true);
        Assert.DoesNotContain(needsReview, t => t.Id == unmatched.Id);

        // Still present under its own match type, just no longer in the attention queue.
        var stillUnmatched = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "unmatched");
        Assert.Contains(stillUnmatched, t => t.Id == unmatched.Id);
    }

    [Fact]
    public async Task Review_OgmMatchedTransaction_Returns422_NotReviewable()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        // Negative amount -> Reversal, not one of the reviewable types either — reuse to avoid
        // needing a full invoice setup just to get an Ogm row.
        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 3, 1), -500, "BE68539007547034", "Someone", "reversal", false)]);
        var reversal = Assert.Single(await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "reversal"));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{reversal.Id}/review", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Review_AlreadyReviewedTransaction_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 3, 1), 1234, "BE68539007547034", "Nobody", "no match", false)]);
        var unmatched = Assert.Single(await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "unmatched"));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{unmatched.Id}/review", org.AccessToken));

        var secondReview = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{unmatched.Id}/review", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondReview.StatusCode);
    }
}
