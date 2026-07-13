using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Children;

public record UpdateChildCommand(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender? Gender,
    string? Nationality,
    string? AllergiesDescription,
    AllergySeverity? AllergySeverity,
    string? MedicalConditions,
    string? DietaryRestrictions,
    string? GpName,
    string? GpPhone,
    string? PediatricianName,
    string? PediatricianPhone,
    string? HealthInsuranceNumber,
    string? Kindcode) : IRequest<ChildResult>;
