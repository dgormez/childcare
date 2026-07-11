using ChildCare.Contracts.Responses;

namespace ChildCare.Application.GroupActivities;

public class GroupActivityResult
{
    public GroupActivityResponse? Response { get; private init; }
    public GroupActivityFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static GroupActivityResult Success(GroupActivityResponse response) => new() { Response = response };
    public static GroupActivityResult Fail(GroupActivityFailure failure) => new() { Failure = failure };
}

public enum GroupActivityFailure
{
    NotFound,
    PhotoLimitReached,
    PhotoTooLarge,
}

public class GroupActivityPhotoResult
{
    public GroupActivityPhotoResponse? Response { get; private init; }
    public GroupActivityFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static GroupActivityPhotoResult Success(GroupActivityPhotoResponse response) => new() { Response = response };
    public static GroupActivityPhotoResult Fail(GroupActivityFailure failure) => new() { Failure = failure };
}
