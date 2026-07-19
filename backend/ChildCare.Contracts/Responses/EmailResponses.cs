namespace ChildCare.Contracts.Responses;

public record BulkEmailUploadUrlResponse(string UploadUrl, string ObjectPath);

public record BulkEmailSendResultResponse(
    Guid BulkEmailSendId,
    int SentCount,
    int SkippedNoEmailCount,
    int ProviderFailureCount);

public record BulkEmailRecipientCountResponse(int RecipientCount);

public record DailyReportResendResultResponse(int SentCount, int SkippedNoEmailCount);

public record UnsubscribeResultResponse(bool Unsubscribed);
