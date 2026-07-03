namespace ChildCare.Contracts.Responses;

public record RegisterOrganisationResponse(
    string AccessToken,
    OrganisationSummary Organisation,
    DirectorSummary Director);

public record OrganisationSummary(Guid Id, string Name, string Slug, string Plan);

public record DirectorSummary(Guid Id, string Email, string Name);
