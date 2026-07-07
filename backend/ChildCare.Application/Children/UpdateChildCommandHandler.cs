using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public class UpdateChildCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<UpdateChildCommand, ChildResult>
{
    public async Task<ChildResult> Handle(UpdateChildCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        child.FirstName = request.FirstName;
        child.LastName = request.LastName;
        child.DateOfBirth = request.DateOfBirth;
        child.Gender = request.Gender;
        child.Nationality = request.Nationality;
        child.AllergiesDescription = request.AllergiesDescription;
        child.AllergySeverity = request.AllergySeverity;
        child.MedicalConditions = request.MedicalConditions;
        child.DietaryRestrictions = request.DietaryRestrictions;
        child.GpName = request.GpName;
        child.GpPhone = request.GpPhone;
        child.HealthInsuranceNumber = request.HealthInsuranceNumber;
        child.Kindcode = request.Kindcode;
        child.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
