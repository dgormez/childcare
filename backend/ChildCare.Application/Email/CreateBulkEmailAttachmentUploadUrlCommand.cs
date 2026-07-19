using ChildCare.Application.Common;
using MediatR;

namespace ChildCare.Application.Email;

public record CreateBulkEmailAttachmentUploadUrlCommand(string ContentType) : IRequest<BulkEmailAttachmentUploadUrlResult>;

public enum BulkEmailAttachmentFailure
{
    InvalidContentType,
}

public class BulkEmailAttachmentUploadUrlResult
{
    public string? UploadUrl { get; private init; }
    public string? ObjectPath { get; private init; }
    public BulkEmailAttachmentFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static BulkEmailAttachmentUploadUrlResult Success(string uploadUrl, string objectPath) => new() { UploadUrl = uploadUrl, ObjectPath = objectPath };
    public static BulkEmailAttachmentUploadUrlResult Fail(BulkEmailAttachmentFailure failure) => new() { Failure = failure };
}

/// <summary>
/// FR-003/FR-017: issues a signed GCS upload URL for a bulk email's optional attachment
/// (research.md R3). A fresh, random id is minted here (rather than reusing the eventual
/// `BulkEmailSend.Id`, not yet known — the send hasn't been composed) as the object-path subject
/// id; `SendBulkEmailCommand` receives the resulting `objectPath` as part of the compose request.
/// </summary>
public class CreateBulkEmailAttachmentUploadUrlCommandHandler(IBulkEmailAttachmentStorage storage)
    : IRequestHandler<CreateBulkEmailAttachmentUploadUrlCommand, BulkEmailAttachmentUploadUrlResult>
{
    private static readonly HashSet<string> AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<BulkEmailAttachmentUploadUrlResult> Handle(CreateBulkEmailAttachmentUploadUrlCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(request.ContentType))
            return BulkEmailAttachmentUploadUrlResult.Fail(BulkEmailAttachmentFailure.InvalidContentType);

        var (objectPath, uploadUrl) = await storage.CreateUploadUrlAsync(Guid.NewGuid(), request.ContentType, cancellationToken);
        return BulkEmailAttachmentUploadUrlResult.Success(uploadUrl, objectPath);
    }
}
