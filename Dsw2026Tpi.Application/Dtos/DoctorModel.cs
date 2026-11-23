namespace Dsw2026Tpi.Application.Dtos;

public record DoctorModel
{
    public record Request(string Name, string LicenseNumber, Guid SpecialtyId);
    public record Response(Guid Id, string Name, string LicenseNumber, SpecialtyDto? Specialty, string AvailabilityStatus);
    public record SpecialtyDto(Guid? Id, string? Name, bool IsActive);
}
