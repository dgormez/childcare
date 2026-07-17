using ChildCare.Application.DevelopmentalMilestones;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>Pure unit tests — no DB — for the shared age-band + grouping logic (research.md R2).</summary>
public class MilestonePortfolioBuilderTests
{
    private static readonly Guid DomainId = Guid.NewGuid();

    private static DevelopmentalDomain Domain() => new()
    {
        Id = DomainId, Code = "language", NameNl = "Taal", NameFr = "Langage", NameEn = "Language", SortOrder = 1,
    };

    private static DevelopmentalMilestone Milestone(Guid id, int from, int to, int sort = 1) => new()
    {
        Id = id, DomainId = DomainId, AgeFromMonths = from, AgeToMonths = to,
        DescriptionNl = "Nl", DescriptionFr = "Fr", DescriptionEn = "En", SortOrder = sort,
    };

    [Theory]
    [InlineData(15)]
    [InlineData(21)]
    [InlineData(18)]
    public void Build_FlagsIsCurrentFocus_AtInclusiveBandBoundariesAndMidpoint(int ageInMonths)
    {
        var milestoneId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-ageInMonths));

        var portfolio = MilestonePortfolioBuilder.Build(
            childId, dob, DateOnly.FromDateTime(DateTime.UtcNow),
            [Domain()], [Milestone(milestoneId, 15, 21)], [], includeHistory: false);

        var milestone = Assert.Single(Assert.Single(portfolio.Domains).Milestones);
        Assert.True(milestone.IsCurrentFocus);
    }

    [Fact]
    public void Build_DoesNotFlagIsCurrentFocus_OutsideBand()
    {
        var milestoneId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-22));

        var portfolio = MilestonePortfolioBuilder.Build(
            childId, dob, DateOnly.FromDateTime(DateTime.UtcNow),
            [Domain()], [Milestone(milestoneId, 15, 21)], [], includeHistory: false);

        var milestone = Assert.Single(Assert.Single(portfolio.Domains).Milestones);
        Assert.False(milestone.IsCurrentFocus);
    }

    [Fact]
    public void Build_WithNoObservations_ReportsNullCurrentStatus_NotAnError()
    {
        var milestoneId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-18));

        var portfolio = MilestonePortfolioBuilder.Build(
            childId, dob, DateOnly.FromDateTime(DateTime.UtcNow),
            [Domain()], [Milestone(milestoneId, 15, 21)], [], includeHistory: true);

        var milestone = Assert.Single(Assert.Single(portfolio.Domains).Milestones);
        Assert.Null(milestone.CurrentStatus);
        Assert.Empty(milestone.History!);
    }

    [Fact]
    public void Build_CurrentStatus_IsTheMostRecentObservation_NotTheFirstRecorded()
    {
        var milestoneId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-18));

        var older = new ChildMilestoneObservation
        {
            ChildId = childId, MilestoneId = milestoneId, Status = MilestoneObservationStatus.Achieved,
            ObservedAt = new DateOnly(2026, 1, 1), CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var newer = new ChildMilestoneObservation
        {
            ChildId = childId, MilestoneId = milestoneId, Status = MilestoneObservationStatus.NotYet,
            ObservedAt = new DateOnly(2026, 2, 1), CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var portfolio = MilestonePortfolioBuilder.Build(
            childId, dob, DateOnly.FromDateTime(DateTime.UtcNow),
            [Domain()], [Milestone(milestoneId, 15, 21)], [older, newer], includeHistory: true);

        var milestone = Assert.Single(Assert.Single(portfolio.Domains).Milestones);
        Assert.Equal("not_yet", milestone.CurrentStatus);
        Assert.Equal(2, milestone.History!.Count); // both preserved (spec.md FR-003)
    }

    [Fact]
    public void Build_WithIncludeHistoryFalse_OmitsHistory()
    {
        var milestoneId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-18));
        var observation = new ChildMilestoneObservation
        {
            ChildId = childId, MilestoneId = milestoneId, Status = MilestoneObservationStatus.Achieved,
            ObservedAt = new DateOnly(2026, 1, 1),
        };

        var portfolio = MilestonePortfolioBuilder.Build(
            childId, dob, DateOnly.FromDateTime(DateTime.UtcNow),
            [Domain()], [Milestone(milestoneId, 15, 21)], [observation], includeHistory: false);

        var milestone = Assert.Single(Assert.Single(portfolio.Domains).Milestones);
        Assert.Equal("achieved", milestone.CurrentStatus);
        Assert.Null(milestone.History);
    }
}
