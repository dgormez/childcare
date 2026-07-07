using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using MediatR;

namespace ChildCare.Application.Children;

public class CreateChildCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<CreateChildCommand, ChildResult>
{
    public async Task<ChildResult> Handle(CreateChildCommand request, CancellationToken cancellationToken)
    {
        var child = new Child
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Nationality = request.Nationality,
            AllergiesDescription = request.AllergiesDescription,
            AllergySeverity = request.AllergySeverity,
            MedicalConditions = request.MedicalConditions,
            DietaryRestrictions = request.DietaryRestrictions,
            GpName = request.GpName,
            GpPhone = request.GpPhone,
            HealthInsuranceNumber = request.HealthInsuranceNumber,
            Kindcode = request.Kindcode,
        };

        db.Children.Add(child);
        await db.SaveChangesAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
