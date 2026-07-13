using ChildCare.Contracts.Responses;

namespace ChildCare.Application.MealPreferences;

public class MealPreferenceResult
{
    public MealPreferenceResponse? Response { get; private init; }
    public MealPreferenceFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static MealPreferenceResult Success(MealPreferenceResponse response) => new() { Response = response };
    public static MealPreferenceResult Fail(MealPreferenceFailure failure) => new() { Failure = failure };
}

public enum MealPreferenceFailure
{
    ChildNotFound,
}
