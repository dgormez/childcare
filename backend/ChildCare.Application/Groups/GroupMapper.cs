using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Groups;

internal static class GroupMapper
{
    public static GroupResponse ToResponse(Group g) => new(g.Id, g.Name, g.LocationId);

    public static ChildGroupAssignmentResponse ToAssignmentResponse(ChildGroupAssignment a, string groupName) => new(
        a.GroupId, groupName, a.StartDate, a.EndDate);
}
