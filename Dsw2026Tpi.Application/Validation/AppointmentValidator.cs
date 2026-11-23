using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Application.Validation;

public class AppointmentValidator
{
    public static void ValidateCreate(Doctor? doctor, AvailabilitySlot? availabilitySlot, Patient? patient, string? reason)
    {
        var errores = new List<(string, string)>();

        if (doctor is null)
        {
            errores.Add(("doctorId", "No existe el Doctor con ese ID"));
        }
        if (availabilitySlot is null)
        {
            errores.Add(("availabilityId", "No existe un slot asociado a ese ID"));
        }
        else if (availabilitySlot.Status != AvailabilitySlotStatus.AVAILABLE)
        {
            errores.Add(("availabilityId", "El turno no se encuentra disponible"));
        }
        if (availabilitySlot is not null && availabilitySlot.Date.ToDateTime(availabilitySlot.StartTime) <= DateTime.Now)
        {
            errores.Add(("availabilityId", "No se pueden reservar turnos en el pasado"));
        }
        if (patient is null)
        {
            errores.Add(("patient.dni", "No existe un paciente con ese DNI"));
        }
        else if (string.IsNullOrWhiteSpace(patient.Dni) || patient.Dni.Length < 7 || patient.Dni.Length > 10)
        {
            errores.Add(("patient.dni", "El DNI tiene que tener entre 7 y 10 dígitos"));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
        {
            errores.Add(("reason", "La razón tiene que tener por lo menos 5 caracteres"));
        }
        if (errores.Any())
        {
            throw new ValidationException("Se encontraron errores en los datos enviados", nameof(ErrorCodes.VALIDATION_ERROR))
                     .WithDetail(errores);
        }
    }

    public static void ValidateDelete(Appointment? appointment)
    {
        var errores = new List<(string, string)>();

        if (appointment is null)
        {
            errores.Add(("appointment", "La cita no existe"));
        }
        else if (appointment.Status != AppointmentStatus.BOOKED)
        {
            errores.Add(("appointment", "Solo se puede cancelar un turno en estado BOOKED"));
        }
        if (errores.Any())
        {
            throw new ValidationException("Se encontraron errores en los datos enviados", nameof(ErrorCodes.VALIDATION_ERROR))
                     .WithDetail(errores);
        }
    }
}
