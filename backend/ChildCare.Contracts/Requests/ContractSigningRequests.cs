namespace ChildCare.Contracts.Requests;

// Feature 024-esignature. SignatureType is a string over-the-wire ("Drawn"/"Typed") to match
// this codebase's convention of not leaking a raw enum type across the API boundary.
public record SubmitContractSigningRequest(
    string SignatureType,
    string SignatureData,
    string SepaIban);
