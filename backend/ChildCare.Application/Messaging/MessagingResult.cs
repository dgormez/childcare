using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Messaging;

public enum MessagingFailure
{
    ChildNotFound,
    NotParticipant,
    ThreadNotFound,
}

public class MessageThreadResult
{
    public bool Succeeded { get; init; }
    public MessagingFailure? Failure { get; init; }
    public MessageThreadResponse? Response { get; init; }

    public static MessageThreadResult Success(MessageThreadResponse response) => new() { Succeeded = true, Response = response };
    public static MessageThreadResult Fail(MessagingFailure failure) => new() { Failure = failure };
}

public class MessageThreadListResult
{
    public bool Succeeded { get; init; }
    public MessagingFailure? Failure { get; init; }
    public IReadOnlyList<MessageThreadSummaryResponse> Threads { get; init; } = [];

    public static MessageThreadListResult Success(IReadOnlyList<MessageThreadSummaryResponse> threads) => new() { Succeeded = true, Threads = threads };
    public static MessageThreadListResult Fail(MessagingFailure failure) => new() { Failure = failure };
}

public class SendMessageResult
{
    public bool Succeeded { get; init; }
    public MessagingFailure? Failure { get; init; }
    public MessageResponse? Response { get; init; }

    public static SendMessageResult Success(MessageResponse response) => new() { Succeeded = true, Response = response };
    public static SendMessageResult Fail(MessagingFailure failure) => new() { Failure = failure };
}
