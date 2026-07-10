using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Announcements;

// LocationNotFound/GroupNotFound are deliberately not members here — those checks run in
// SendAnnouncementCommandValidator via FluentValidation (422 errors.validation), mirroring
// ParentInvitationFailure's precedent for the same reason.
public enum AnnouncementFailure
{
    NotFound,
    NotRecipient,
}

public class AnnouncementResult
{
    public bool Succeeded { get; init; }
    public AnnouncementFailure? Failure { get; init; }
    public AnnouncementResponse? Response { get; init; }

    public static AnnouncementResult Success(AnnouncementResponse response) => new() { Succeeded = true, Response = response };
    public static AnnouncementResult Fail(AnnouncementFailure failure) => new() { Failure = failure };
}

public class ParentAnnouncementResult
{
    public bool Succeeded { get; init; }
    public AnnouncementFailure? Failure { get; init; }
    public ParentAnnouncementResponse? Response { get; init; }

    public static ParentAnnouncementResult Success(ParentAnnouncementResponse response) => new() { Succeeded = true, Response = response };
    public static ParentAnnouncementResult Fail(AnnouncementFailure failure) => new() { Failure = failure };
}
