using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public record PublicEnrollmentLocationInfo(string LocationName, bool Enabled, string DefaultLocale);

public class PublicEnrollmentLocationInfoResult
{
    public bool Succeeded { get; init; }
    public PublicEnrollmentFailure? Failure { get; init; }
    public PublicEnrollmentLocationInfo? Info { get; init; }

    public static PublicEnrollmentLocationInfoResult Success(PublicEnrollmentLocationInfo info) => new() { Succeeded = true, Info = info };
    public static PublicEnrollmentLocationInfoResult Fail(PublicEnrollmentFailure failure) => new() { Failure = failure };
}

/// <summary>
/// FR-021: exposes only what the public form needs to render itself or its disabled state —
/// no capacity, contact, or other tenant data.
/// </summary>
public record GetPublicEnrollmentLocationInfoQuery(string OrganisationSlug, string LocationSlug) : IRequest<PublicEnrollmentLocationInfoResult>;

public class GetPublicEnrollmentLocationInfoQueryHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver) : IRequestHandler<GetPublicEnrollmentLocationInfoQuery, PublicEnrollmentLocationInfoResult>
{
    public async Task<PublicEnrollmentLocationInfoResult> Handle(GetPublicEnrollmentLocationInfoQuery request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return PublicEnrollmentLocationInfoResult.Fail(PublicEnrollmentFailure.NotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var location = await db.Locations.FirstOrDefaultAsync(
            l => l.PublicEnrollmentSlug == request.LocationSlug && l.DeactivatedAt == null, cancellationToken);
        if (location is null)
            return PublicEnrollmentLocationInfoResult.Fail(PublicEnrollmentFailure.NotFound);

        return PublicEnrollmentLocationInfoResult.Success(
            new PublicEnrollmentLocationInfo(location.Name, location.PublicEnrollmentEnabled, location.DefaultEnrollmentLocale));
    }
}
