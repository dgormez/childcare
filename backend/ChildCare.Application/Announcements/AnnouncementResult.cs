using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Announcements;

public enum AnnouncementFailure
{
    LocationNotFound,
    GroupNotFound,
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

public class AnnouncementListResult
{
    public bool Succeeded { get; init; }
    public AnnouncementFailure? Failure { get; init; }
    public IReadOnlyList<AnnouncementResponse> Announcements { get; init; } = [];

    public static AnnouncementListResult Success(IReadOnlyList<AnnouncementResponse> announcements) => new() { Succeeded = true, Announcements = announcements };
    public static AnnouncementListResult Fail(AnnouncementFailure failure) => new() { Failure = failure };
}

public class ParentAnnouncementResult
{
    public bool Succeeded { get; init; }
    public AnnouncementFailure? Failure { get; init; }
    public ParentAnnouncementResponse? Response { get; init; }

    public static ParentAnnouncementResult Success(ParentAnnouncementResponse response) => new() { Succeeded = true, Response = response };
    public static ParentAnnouncementResult Fail(AnnouncementFailure failure) => new() { Failure = failure };
}
