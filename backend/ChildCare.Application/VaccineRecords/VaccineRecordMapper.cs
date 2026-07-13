using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.VaccineRecords;

internal static class VaccineRecordMapper
{
    public static async Task<VaccineRecordResponse> ToResponseAsync(VaccineRecord v, IHealthAttachmentStorage storage, CancellationToken cancellationToken)
    {
        var attachmentDownloadUrl = await storage.CreateDownloadUrlAsync(v.AttachmentObjectPath, cancellationToken);

        return new VaccineRecordResponse(
            v.Id,
            v.ChildId,
            v.VaccineName,
            v.VaccineTypeId,
            attachmentDownloadUrl,
            v.DoseNumber,
            v.AdministeredOn,
            v.NextDueDate,
            v.AdministeredBy,
            v.Notes,
            v.RecordedBy,
            v.CreatedAt,
            v.UpdatedAt);
    }
}
