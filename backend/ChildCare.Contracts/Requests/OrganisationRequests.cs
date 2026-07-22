namespace ChildCare.Contracts.Requests;

// Feature 014 — contracts/014-invoicing/invoicing-api.md.
// Feature 024-esignature (User Story 4) adds SepaCreditorIdentifier — full-replacement PUT, see
// UpdateOrganisationCommand.
public record UpdateOrganisationRequest(string? KboNumber, string? SepaCreditorIdentifier);
