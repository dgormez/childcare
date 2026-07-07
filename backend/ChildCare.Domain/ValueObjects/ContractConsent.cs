namespace ChildCare.Domain.ValueObjects;

// Owned JSONB object on Contract.Consent (research.md R1) — replaces a single photo_consent
// boolean with five independent choices (FR-010). Any flag not explicitly set defaults to
// false — consent for photographing/filming minors is never inferred.
public class ContractConsent
{
    public bool PhotosInternal    { get; set; }
    public bool PhotosWebsite     { get; set; }
    public bool PhotosSocialMedia { get; set; }
    public bool VideoInternal     { get; set; }
    public bool PhotosPress       { get; set; }
}
