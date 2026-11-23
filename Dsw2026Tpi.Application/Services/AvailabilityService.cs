using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Utils;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IPersistence _persistence;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(IPersistence persistence, ILogger<AvailabilityService> logger)
    {
        _persistence = persistence;
        _logger = logger;
    }

    private static void ValidateDays(List<AvailabilityModel.DaySchedule>? days)
    {
        var errores = new List<(string, string)>();

        if (days == null || days.Count == 0)
        {
            errores.Add(("days", "Debe especificar al menos un día de disponibilidad."));
        }
        else
        {
            var seenDays = new HashSet<DayOfWeek>();

            foreach (var daySchedule in days)
            {
                var dayOfWeek = daySchedule.Day.toDayOfWeek();

                if (!seenDays.Add(dayOfWeek))
                {
                    errores.Add(("days", $"El día '{daySchedule.Day}' está duplicado o solapado."));
                }

                if (daySchedule.EndTime < daySchedule.StartTime.AddMinutes(30))
                {
                    errores.Add(("startTime", $"En {daySchedule.Day}, el horario de salida debe ser al menos 30 minutos posterior al de entrada."));
                }
            }
        }

        if (errores.Any())
        {
            throw new ValidationException(
                "Se encontraron errores en los horarios enviados.",
                nameof(ErrorCodes.VALIDATION_ERROR)
            ).WithDetail(errores);
        }
    }

    private List<AvailabilitySlot> GenerateSlots(
        Guid doctorId,
        TimeOnly startTime,
        TimeOnly endTime,
        DayOfWeek dayOfWeek,
        DateOnly actualDate,
        int numOfDays)
    {
        var slots = new List<AvailabilitySlot>();

        for (int d = actualDate.Day; d <= numOfDays; d++)
        {
            var iterationDate = new DateOnly(actualDate.Year, actualDate.Month, d);

            if (iterationDate.DayOfWeek == dayOfWeek && !iterationDate.IsHoliday())
            {
                var slotStartTime = startTime;
                while (slotStartTime < endTime)
                {
                    var slotEndTime = slotStartTime.AddMinutes(30);

                    if (slotEndTime > endTime)
                    {
                        break;
                    }

                    slots.Add(new AvailabilitySlot
                    {
                        Date = iterationDate,
                        StartTime = slotStartTime,
                        EndTime = slotEndTime,
                        DoctorId = doctorId
                    });
                    slotStartTime = slotEndTime;
                }
            }
        }

        return slots;
    }

    public async Task<AvailabilityModel.Response> CreateAvailabilitiesAsync(AvailabilityModel.Request request)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Los datos de disponibilidad son obligatorios.",
                nameof(ErrorCodes.VALIDATION_ERROR));
        }

        ValidateDays(request.Days);

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId, "AvailabilityRules");
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        var actualDate = DateOnly.FromDateTime(DateTime.Now);
        var hasCurrentMonthRules = doctor.AvailabilityRules != null &&
            doctor.AvailabilityRules.Any(r => !r.Deleted && r.Year == actualDate.Year && r.Month == actualDate.Month);

        if (hasCurrentMonthRules)
        {
            throw new ConflictException(nameof(ErrorCodes.AVAILABILITY_CONFLICT), ErrorCodes.AVAILABILITY_CONFLICT);
        }

        var numOfDays = DateTime.DaysInMonth(actualDate.Year, actualDate.Month);
        var avaRules = new List<Availability>();

        foreach (var daySchedule in request.Days)
        {
            var dayOfWeek = daySchedule.Day.toDayOfWeek();
            var avaRule = new Availability
            {
                DoctorId = doctor.Id,
                Month = (byte)actualDate.Month,
                Year = (short)actualDate.Year,
                DayOfWeek = (byte)dayOfWeek,
                StartTime = daySchedule.StartTime,
                EndTime = daySchedule.EndTime,
                Slots = GenerateSlots(
                    request.DoctorId,
                    daySchedule.StartTime,
                    daySchedule.EndTime,
                    dayOfWeek,
                    actualDate,
                    numOfDays)
            };

            avaRules.Add(avaRule);
        }

        if (avaRules.Any())
        {
            await _persistence.AddRange(avaRules);
        }

        _logger.LogInformation(
            "Disponibilidad mensual creada para el médico {DoctorId} con {RulesCount} días configurados.",
            doctor.Id,
            avaRules.Count);

        return new AvailabilityModel.Response(doctor.Id, request.Days);
    }

    public async Task<AvailabilityModel.Response> UpdateAvailabilitiesAsync(AvailabilityModel.Request request)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Los datos de disponibilidad son obligatorios.",
                nameof(ErrorCodes.VALIDATION_ERROR));
        }

        ValidateDays(request.Days);

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        var actualDate = DateOnly.FromDateTime(DateTime.Now);
        var numOfDays = DateTime.DaysInMonth(actualDate.Year, actualDate.Month);

        var existingRules = (await _persistence.GetFiltered<Availability>(
            a => a.DoctorId == request.DoctorId && a.Year == actualDate.Year && a.Month == actualDate.Month,
            nameof(Availability.Slots)))?.ToList() ?? [];

        if (!existingRules.Any())
        {
            return await CreateAvailabilitiesAsync(request);
        }

        var requestedByDay = request.Days.ToDictionary(d => (byte)d.Day.toDayOfWeek(), d => d);
        var rulesToUpdate = new List<Availability>();
        var rulesToAdd = new List<Availability>();

        foreach (var rule in existingRules)
        {
            var preservedSlots = rule.Slots
                .Where(s => s.Status != AvailabilitySlotStatus.AVAILABLE || s.Date < actualDate)
                .ToList();

            if (requestedByDay.TryGetValue(rule.DayOfWeek, out var daySchedule))
            {
                rule.Activate();
                rule.StartTime = daySchedule.StartTime;
                rule.EndTime = daySchedule.EndTime;
                rule.Slots.Clear();

                var generated = GenerateSlots(
                    request.DoctorId,
                    daySchedule.StartTime,
                    daySchedule.EndTime,
                    (DayOfWeek)rule.DayOfWeek,
                    actualDate,
                    numOfDays);

                foreach (var slot in generated)
                {
                    bool alreadyPreserved = preservedSlots.Any(p => p.Date == slot.Date && p.StartTime == slot.StartTime);
                    if (!alreadyPreserved)
                    {
                        rule.Slots.Add(slot);
                    }
                }

                foreach (var preserved in preservedSlots)
                {
                    rule.Slots.Add(preserved);
                }
            }
            else
            {
                rule.Delete();
                rule.Slots.Clear();
                foreach (var preserved in preservedSlots)
                {
                    rule.Slots.Add(preserved);
                }
            }

            rulesToUpdate.Add(rule);
        }

        foreach (var kvp in requestedByDay)
        {
            if (!existingRules.Any(r => r.DayOfWeek == kvp.Key))
            {
                var newRule = new Availability
                {
                    DoctorId = request.DoctorId,
                    Month = (byte)actualDate.Month,
                    Year = (short)actualDate.Year,
                    DayOfWeek = kvp.Key,
                    StartTime = kvp.Value.StartTime,
                    EndTime = kvp.Value.EndTime,
                    Slots = GenerateSlots(
                        request.DoctorId,
                        kvp.Value.StartTime,
                        kvp.Value.EndTime,
                        (DayOfWeek)kvp.Key,
                        actualDate,
                        numOfDays)
                };
                rulesToAdd.Add(newRule);
            }
        }

        if (rulesToUpdate.Any())
        {
            await _persistence.UpdateRange(rulesToUpdate);
        }

        if (rulesToAdd.Any())
        {
            await _persistence.AddRange(rulesToAdd);
        }

        _logger.LogInformation(
            "Disponibilidad mensual actualizada para el médico {DoctorId}: {UpdatedCount} reglas actualizadas, {AddedCount} agregadas.",
            request.DoctorId,
            rulesToUpdate.Count,
            rulesToAdd.Count);

        return new AvailabilityModel.Response(request.DoctorId, request.Days);
    }
}
