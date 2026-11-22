using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Validation;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IPersistence _persistence;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(IPersistence persistence, ILogger<AppointmentService> logger)
    {
        _persistence = persistence;
        _logger = logger;
    }

    public async Task<AppointmentModel.Response> CreateAppointment(AppointmentModel.Request request)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Los datos de la reserva son obligatorios.",
                nameof(ErrorCodes.VALIDATION_ERROR));
        }

        var doctor = await _persistence.GetById<Doctor>(
            request.DoctorId,
            "AvailabilityRules",
            "AvailabilityRules.Slots",
            nameof(Doctor.Speciality));

        var availabilitySlot = await _persistence.First<AvailabilitySlot>(
            s => s.Id == request.AvailabilityId && s.Availability.DoctorId == request.DoctorId);

        Patient? patient = null;
        if (request.Patient != null)
        {
            string patientDni = request.Patient.Dni.ToString();
            patient = await _persistence.First<Patient>(p => p.Dni == patientDni);
        }

        AppointmentValidator.ValidateCreate(doctor, availabilitySlot, patient, request.Reason);

        var newAppointment = new Appointment
        {
            Reason = request.Reason.Trim(),
            Patient = patient!,
            PatientId = patient!.Id,
            AvailabilitySlot = availabilitySlot!,
            AvailabilitySlotId = availabilitySlot!.Id,
        };
        newAppointment.Book();

        try
        {
            await _persistence.Add(newAppointment);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Conflicto de concurrencia al reservar slot {SlotId} para paciente DNI {PatientDni}.",
                availabilitySlot.Id,
                patient.Dni);
            throw new ConflictException(nameof(ErrorCodes.APPOINTMENT_CONFLICT), ErrorCodes.APPOINTMENT_CONFLICT);
        }

        _logger.LogInformation(
            "Turno reservado {AppointmentId} para paciente DNI {PatientDni} con médico {DoctorId} el {Date} a las {StartTime}.",
            newAppointment.Id,
            patient.Dni,
            doctor!.Id,
            availabilitySlot.Date,
            availabilitySlot.StartTime);

        return new AppointmentModel.Response(
            newAppointment.Id,
            newAppointment.Status.ToString(),
            availabilitySlot.Date,
            availabilitySlot.StartTime,
            newAppointment.Reason,
            new AppointmentModel.PatientDto(patient.Dni, patient.Name ?? string.Empty),
            new AppointmentModel.DoctorDto(
                doctor.Id,
                doctor.Name,
                new AppointmentModel.SpecialtyDto(doctor.Speciality.Id, doctor.Speciality.Name))
        );
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetPatientAppointmentsAsync(string dni)
    {
        var trimmedDni = dni?.Trim();
        if (!trimmedDni.IsDniValid())
        {
            throw new ValidationException("Se encontraron errores en los datos enviados", nameof(ErrorCodes.VALIDATION_ERROR))
                .WithDetail("dni", "El DNI debe tener entre 7 y 8 dígitos numéricos.");
        }

        var patient = await _persistence.First<Patient>(p => p.Dni == trimmedDni);
        if (patient == null)
        {
            return [];
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.PatientId == patient.Id &&
                 a.Status == AppointmentStatus.BOOKED &&
                 (a.AvailabilitySlot.Date > today ||
                  (a.AvailabilitySlot.Date == today && a.AvailabilitySlot.StartTime > currentTime)),
            "Patient",
            "AvailabilitySlot",
            "AvailabilitySlot.Availability",
            "AvailabilitySlot.Availability.Doctor",
            "AvailabilitySlot.Availability.Doctor.Speciality");

        if (appointments == null)
        {
            return [];
        }

        var result = appointments
            .OrderBy(a => a.AvailabilitySlot.Date)
            .ThenBy(a => a.AvailabilitySlot.StartTime)
            .Select(a => new AppointmentModel.Response(
                a.Id,
                a.Status.ToString(),
                a.AvailabilitySlot.Date,
                a.AvailabilitySlot.StartTime,
                a.Reason,
                new AppointmentModel.PatientDto(a.Patient.Dni, a.Patient.Name ?? string.Empty),
                new AppointmentModel.DoctorDto(
                    a.AvailabilitySlot.DoctorId,
                    a.AvailabilitySlot.Availability.Doctor.Name,
                    new AppointmentModel.SpecialtyDto(
                        a.AvailabilitySlot.Availability.Doctor.SpecialityId,
                        a.AvailabilitySlot.Availability.Doctor.Speciality.Name))))
            .ToList();

        _logger.LogInformation(
            "Consulta de turnos activos para paciente DNI {PatientDni}: {Count} turnos encontrados.",
            trimmedDni,
            result.Count);

        return result;
    }

    public async Task CancelAppointmentAsync(Guid id)
    {
        var appointment = await _persistence.GetById<Appointment>(id, "AvailabilitySlot");
        AppointmentValidator.ValidateDelete(appointment);
        appointment!.Cancel();
        await _persistence.Update(appointment);

        _logger.LogInformation("Turno cancelado {AppointmentId}.", id);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetAppointmentsByDate(DateOnly date)
    {
        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.AvailabilitySlot.Date == date,
            "Patient",
            "AvailabilitySlot",
            "AvailabilitySlot.Availability",
            "AvailabilitySlot.Availability.Doctor",
            "AvailabilitySlot.Availability.Doctor.Speciality");

        if (appointments == null || !appointments.Any())
        {
            _logger.LogInformation("Consulta de turnos para la fecha {Date}: 0 turnos encontrados.", date);
            return [];
        }

        var response = appointments
            .OrderBy(a => a.AvailabilitySlot.StartTime)
            .Select(a => new AppointmentModel.Response(
                a.Id,
                a.Status.ToString(),
                a.AvailabilitySlot.Date,
                a.AvailabilitySlot.StartTime,
                a.Reason,
                new AppointmentModel.PatientDto(a.Patient.Dni, a.Patient.Name ?? string.Empty),
                new AppointmentModel.DoctorDto(
                    a.AvailabilitySlot.DoctorId,
                    a.AvailabilitySlot.Availability.Doctor.Name,
                    new AppointmentModel.SpecialtyDto(
                        a.AvailabilitySlot.Availability.Doctor.SpecialityId,
                        a.AvailabilitySlot.Availability.Doctor.Speciality.Name))))
            .ToList();

        _logger.LogInformation("Consulta de turnos para la fecha {Date}: {Count} turnos encontrados.", date, response.Count);

        return response;
    }

    public async Task<Pagination<AppointmentModel.Response>> CombinedSearch(
        int pageSize,
        int pageIndex,
        Guid? specialtyId,
        Guid? doctorId,
        string? dni,
        DateOnly? date)
    {
        var filterDni = dni?.Trim();

        Expression<Func<Appointment, bool>> combinedPredicate = a =>
            (string.IsNullOrEmpty(filterDni) || a.Patient.Dni == filterDni) &&
            (!doctorId.HasValue || a.AvailabilitySlot.Availability.Doctor.Id == doctorId) &&
            (!specialtyId.HasValue || a.AvailabilitySlot.Availability.Doctor.SpecialityId == specialtyId) &&
            (!date.HasValue || a.AvailabilitySlot.Date == date);

        var response = await _persistence.Paginate<Appointment, DateOnly>(
            pageSize,
            pageIndex,
            combinedPredicate,
            r => r.AvailabilitySlot.Date,
            "Patient",
            "AvailabilitySlot",
            "AvailabilitySlot.Availability",
            "AvailabilitySlot.Availability.Doctor",
            "AvailabilitySlot.Availability.Doctor.Speciality");

        _logger.LogInformation(
            "Búsqueda combinada de turnos (SpecialtyId={SpecialtyId}, DoctorId={DoctorId}, Dni={Dni}, Date={Date}): {Total} resultados.",
            specialtyId,
            doctorId,
            filterDni,
            date,
            response.Total);

        return response.Map(r => new AppointmentModel.Response(
            r.Id,
            r.Status.ToString(),
            r.AvailabilitySlot.Date,
            r.AvailabilitySlot.StartTime,
            r.Reason,
            new AppointmentModel.PatientDto(r.Patient.Dni, r.Patient.Name ?? string.Empty),
            new AppointmentModel.DoctorDto(
                r.AvailabilitySlot.DoctorId,
                r.AvailabilitySlot.Availability.Doctor.Name,
                new AppointmentModel.SpecialtyDto(
                    r.AvailabilitySlot.Availability.Doctor.SpecialityId,
                    r.AvailabilitySlot.Availability.Doctor.Speciality.Name))));
    }
}
