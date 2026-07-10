namespace ChildCare.Contracts.Requests;

public record SendAnnouncementRequest(Guid LocationId, Guid? GroupId, string Subject, string Body);
