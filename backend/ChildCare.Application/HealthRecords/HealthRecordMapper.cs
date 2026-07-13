using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.HealthRecords;

internal static class HealthRecordMapper
{
    public static async Task<HealthRecordResponse> ToResponseAsync(HealthRecord r, IHealthAttachmentStorage storage, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isExpired = r.ValidUntil.HasValue && r.ValidUntil.Value < today;
        var attachmentDownloadUrl = await storage.CreateDownloadUrlAsync(r.AttachmentObjectPath, cancellationToken);

        return new HealthRecordResponse(
            r.Id,
            r.ChildId,
            r.RecordType.ToWireString(),
            r.Title,
            r.Description,
            r.ValidFrom,
            r.ValidUntil,
            isExpired,
            attachmentDownloadUrl,
            r.RecordedBy,
            r.CreatedAt,
            r.UpdatedAt);
    }

    public static bool TryParseRecordType(string value, out HealthRecordType type) =>
        HealthRecordTypeExtensions.TryParseWireString(value, out type);
}
