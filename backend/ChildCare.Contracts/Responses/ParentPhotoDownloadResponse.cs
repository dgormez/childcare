namespace ChildCare.Contracts.Responses;

// 031-photo-lifecycle-governance FR-013 — signed URL with an attachment content-disposition,
// 15-minute TTL matching every other signed URL in this codebase.
public record ParentPhotoDownloadResponse(string DownloadUrl, DateTime ExpiresAt);
