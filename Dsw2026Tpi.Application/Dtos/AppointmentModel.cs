namespace Dsw2026Tpi.Application.Dtos;

public record AppointmentModel
{
    public record Request(Guid DoctorId, Guid AvailabilityId, PatientAux? Patient, string Reason);
    public record PatientAux(long Dni);
    public record PatientDto(string Dni, string Name);
    public record DoctorDto(Guid Id, string Name, SpecialtyDto Specialty);
    public record SpecialtyDto(Guid Id, string Name);
    public record Response(
        Guid appointmentId,
        string appointmentStatus,
        DateOnly Date,
        TimeOnly StartTime,
        string Reason,
        PatientDto Patient,
        DoctorDto Doctor
    );
}
