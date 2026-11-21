using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Utils;
using Dsw2026Tpi.Application.Validation;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System.Data;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        var doctors = await _persistence.Paginate<Doctor, string>(pageSize, pageIndex, d => string.IsNullOrWhiteSpace(name) && !d.Deleted ||
                                                   d.Name.Contains(name) && !d.Deleted, x => x.Name, nameof(Doctor.Speciality));

        return doctors.Map(d => new DoctorModel.Response(d.Id, d.Name, d.LicenseNumber,
            new DoctorModel.SpecialtyDto(d.Speciality?.Id, d.Speciality?.Name)));
    }


    //public async Task<Pagination<AvailabilityModel.DayScheduleResponse>> GetAvailabilities(Guid id, int pageSize, int pageIndex)
    //{
    //    var doctor = await _persistence.GetById<Doctor>(id);
    //    if (doctor == null)
    //    {
    //        throw new EntityNotFoundException(nameof(Doctor));
    //    }

    //    //var slots = await _persistence.Paginate<AvailabilitySlot, DateOnly>(pageSize, pageIndex,
    //    //                                                           s => s.Availability.DoctorId == id && s.Status == 0 && s.Date >= DateOnly.FromDateTime(DateTime.Now), s => s.Date);

    //    var rules= await _persistence.Paginate<Availability, byte>(pageSize, pageIndex,
    //                                                               r => r.DoctorId == id, r => r.DayOfWeek);
    //    return rules.Map(r => new AvailabilityModel.Response(r.DoctorId, new List<AvailabilityModel.DaySchedule>))
    //}

    public async Task<List<AvailabilityModel.ResponseAvailabilities>> GetAvailabilities(Guid doctorId) 
    {
        var doctor = await _persistence.GetById<Doctor>(doctorId, "AvailabilityRules");
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }

        var rules = doctor.AvailabilityRules.Select(r => new AvailabilityModel.ResponseAvailabilities(r.Id, r.DayOfWeek.ToString(), r.StartTime, r.EndTime));
        return rules.ToList();
    }
    //TODO: este método es nuevo y tiene más sentido que use la paginación
    public async Task<AvailabilityModel.ResponseSlots> GetSlots(Guid id, string day) 
    {
        var doctor = await _persistence.GetById<Doctor>(id, "AvailabilityRules", "AvailabilityRules.Slots");
        var rule = doctor.AvailabilityRules.FirstOrDefault(rule => rule.DayOfWeek == (byte)day.toDayOfWeek());
        var slots = rule.Slots.Select(s => new AvailabilityModel.Slots(s.Id, s.Date.ToString("dd/MM/yyyy"), s.StartTime, s.EndTime));
        //TODO: añadir verificaciones básicas de que el doctor y la regla existen, y que la regla tiene slots
        return new AvailabilityModel.ResponseSlots(rule.Id, day, slots.ToList());
    }
    public async Task<DoctorModel.Response> CreateDoctor(DoctorModel.Request request)
    {
        DoctorValidator.Validate(request);
        var specialty = await _persistence.GetById<Specialty>(request.SpecialityId);
        if(specialty == null)
        {
            throw new EntityNotFoundException(nameof(Specialty));
        }

        var doctor = new Doctor(request.Name, request.LicenseNumber, request.SpecialityId);
        await _persistence.Add(doctor);

        return new DoctorModel.Response(doctor.Id, doctor.Name, doctor.LicenseNumber,
            new DoctorModel.SpecialtyDto(specialty.Id, specialty.Name));
    }


    public async Task<DoctorModel.Response> UpdateDoctor(Guid id, DoctorModel.Request request)
    {
        var specialty = await _persistence.GetById<Specialty>(request.SpecialityId);
        if (specialty == null)
        {
            throw new EntityNotFoundException(nameof(Specialty));
        }

        var doctor = await _persistence.GetById<Doctor>(id);
        if (doctor == null)
        {
            throw new EntityNotFoundException(nameof(Doctor));
        }
        DoctorValidator.Validate(request);
        doctor.Update(request.Name, request.LicenseNumber, request.SpecialityId);
        await _persistence.Update(doctor);

        return new DoctorModel.Response(doctor.Id, doctor.Name, doctor.LicenseNumber,
            new DoctorModel.SpecialtyDto(specialty.Id, specialty.Name));
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
