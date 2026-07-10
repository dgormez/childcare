namespace ChildCare.Contracts.Requests;

public record CreateMessageThreadRequest(Guid? ChildId, string Subject, string Body);

public record SendMessageRequest(string Body);
