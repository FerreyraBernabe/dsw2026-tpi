using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Utils;
using Dsw2026Tpi.Application.Validation;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(
        int pageSize,
        int pageIndex,
        string? name = null,
        Guid? specialtyId = null)
    {
        DoctorValidator.ValidateNameFilter(name);
        var filterName = name?.Trim();

        var doctors = await _persistence.Paginate<Doctor, string>(
            pageSize,
            pageIndex,
            d => !d.Deleted &&
                 (string.IsNullOrWhiteSpace(filterName) || d.Name.Contains(filterName)) &&
                 (!specialtyId.HasValue || d.SpecialityId == specialtyId.Value),
            x => x.Name,
            nameof(Doctor.Speciality),
            "AvailabilityRules",
            "AvailabilityRules.Slots");

        return doctors.Map(d => new DoctorModel.Response(
            d.Id,
            d.Name,
            d.LicenseNumber,
            new DoctorModel.SpecialtyDto(d.Speciality?.Id, d.Speciality?.Name, d.Speciality?.IsActive ?? false),
            d.AvailabilityStatus()));
    }

    public async Task<List<AvailabilityModel.DayScheduleResponse>> GetAvailabilities(Guid doctorId)
    {
        var doctor = await _persistence.GetById<Doctor>(doctorId);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        var now = DateOnly.FromDateTime(DateTime.Now);
        var rules = await _persistence.GetFiltered<Availability>(
            a => a.DoctorId == doctorId &&
                 !a.Deleted &&
                 a.Year == now.Year &&
                 a.Month == now.Month);

        if (rules == null)
        {
            return [];
        }

        return rules
            .OrderBy(a => a.DayOfWeek)
            .Select(a => new AvailabilityModel.DayScheduleResponse(
                a.Id,
                ((DayOfWeek)a.DayOfWeek).ToSpanishDayName(),
                a.StartTime,
                a.EndTime))
            .ToList();
    }

    public async Task<IEnumerable<AvailabilityModel.SlotResponse>> GetAvailableSlots(
        Guid id,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var doctor = await _persistence.GetById<Doctor>(id);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
        var effectiveFrom = from.HasValue && from.Value > today ? from.Value : today;

        var slots = await _persistence.GetFiltered<AvailabilitySlot>(
            s => s.DoctorId == id &&
                 !s.Deleted &&
                 s.Status == AvailabilitySlotStatus.AVAILABLE &&
                 (s.Date > effectiveFrom || (s.Date == effectiveFrom && (effectiveFrom > today || s.StartTime > currentTime))) &&
                 (!to.HasValue || s.Date <= to.Value));

        if (slots == null)
        {
            return [];
        }

        return slots
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .Select(s => new AvailabilityModel.SlotResponse(
                s.Id,
                s.Date,
                s.StartTime,
                s.EndTime,
                s.Status.ToString()));
    }

    public async Task<DoctorModel.Response> CreateDoctor(DoctorModel.Request request)
    {
        DoctorValidator.Validate(request);
        var specialty = await _persistence.GetById<Specialty>(request.SpecialtyId);
        if (specialty == null)
        {
            throw new EntityNotFoundException(nameof(Specialty));
        }

        var doctor = new Doctor(request.Name.Trim(), request.LicenseNumber?.Trim() ?? string.Empty, request.SpecialtyId);
        await _persistence.Add(doctor);

        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            new DoctorModel.SpecialtyDto(specialty.Id, specialty.Name, specialty.IsActive),
            "Not Available");
    }

    public async Task<DoctorModel.Response> UpdateDoctor(Guid id, DoctorModel.Request request)
    {
        DoctorValidator.Validate(request);
        var specialty = await _persistence.GetById<Specialty>(request.SpecialtyId);
        if (specialty == null)
        {
            throw new EntityNotFoundException(nameof(Specialty));
        }

        var doctor = await _persistence.GetById<Doctor>(id, "AvailabilityRules", "AvailabilityRules.Slots");
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        doctor.Update(request.Name.Trim(), request.LicenseNumber?.Trim() ?? string.Empty, request.SpecialtyId);
        await _persistence.Update(doctor);

        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            new DoctorModel.SpecialtyDto(specialty.Id, specialty.Name, specialty.IsActive),
            doctor.AvailabilityStatus());
    }

    public async Task DeleteDoctor(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        doctor.Delete();
        await _persistence.Update(doctor);
    }

    public async Task ReactivateDoctor(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        doctor.Activate();
        await _persistence.Update(doctor);
    }
}
