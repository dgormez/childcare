namespace ChildCare.Contracts.Responses;

// 031-photo-lifecycle-governance — contracts/photo-lifecycle-api.md. A non-empty
// FailedObjectPaths list MUST be rendered as a failure state by the client (FR-016), even though
// the HTTP status is 200 — the purge request itself succeeded, only some underlying GCS deletes
// did not.
public record PurgePhotosResponse(
    IReadOnlyList<string> DeletedObjectPaths,
    IReadOnlyList<string> FailedObjectPaths,
    int PreservedGroupPhotoCount);
