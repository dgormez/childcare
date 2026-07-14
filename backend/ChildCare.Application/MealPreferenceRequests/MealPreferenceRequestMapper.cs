using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.MealPreferenceRequests;

public enum MealPreferenceChangeRequestFailure
{
    ChildNotLinked,
    DuplicatePendingRequest,
    NotPending,
    ChildNotFound,
}

public class MealPreferenceChangeRequestResult
{
    public MealPreferenceChangeRequestResponse? Response { get; private init; }
    public MealPreferenceChangeRequestFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static MealPreferenceChangeRequestResult Success(MealPreferenceChangeRequestResponse response) => new() { Response = response };
    public static MealPreferenceChangeRequestResult Fail(MealPreferenceChangeRequestFailure failure) => new() { Failure = failure };
}

public static class MealPreferenceRequestMapper
{
    public static MealPreferenceChangeRequestResponse ToResponse(
        MealPreferenceChangeRequest request,
        string childName,
        string requestedByName,
        IReadOnlyList<MealPreferenceRequestHealthRecordEntry> activeHealthRecords) => new(
        Id: request.Id,
        ChildId: request.ChildId,
        ChildName: childName,
        RequestedByName: requestedByName,
        NewTexture: request.NewTexture,
        NewDietaryType: request.NewDietaryType,
        Notes: request.Notes,
        Status: request.Status.ToString().ToLowerInvariant(),
        CreatedAt: request.CreatedAt,
        DecidedAt: request.DecidedAt,
        DecisionNotes: request.DecisionNotes,
        ActiveHealthRecords: activeHealthRecords);
}
