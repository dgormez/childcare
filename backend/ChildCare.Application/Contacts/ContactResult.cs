using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Contacts;

public class ContactResult
{
    public ContactResponse? Response { get; private init; }
    public ContactFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ContactResult Success(ContactResponse response) => new() { Response = response };

    /// <summary>Success with no payload — used by UnlinkContactFromChildCommand (contracts/children-api.md: 200, no body).</summary>
    public static ContactResult Success() => new();
    public static ContactResult Fail(ContactFailure failure) => new() { Failure = failure };
}

public enum ContactFailure
{
    NotFound,
    ChildNotFound,
    LinkAlreadyExists,
}

/// <summary>Separate result for endpoints returning a ChildContactResponse instead of a ContactResponse.</summary>
public class ChildContactResult
{
    public ChildContactResponse? Response { get; private init; }
    public ContactFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ChildContactResult Success(ChildContactResponse response) => new() { Response = response };
    public static ChildContactResult Fail(ContactFailure failure) => new() { Failure = failure };
}
