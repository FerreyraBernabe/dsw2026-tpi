using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Dtos;

public record AvailabilityModel
{
    public record Request(Guid DoctorId, List<DaySchedule> Days);
    public record Response(Guid DoctorId, List<DaySchedule> Days); 
    public record DaySchedule(string Day, TimeOnly StartTime, TimeOnly EndTime);
    public record DayScheduleResponse(Guid Id, string Day, TimeOnly StartTime, TimeOnly EndTime);
    
    //nuevo
    public record RequestAvailabilities(Guid DoctorId);
    public record RequestSlots(Guid DoctorId, string Day);
    public record ResponseAvailabilities(Guid AvailabilityId, string Day, TimeOnly StartTime, TimeOnly EndTime);
    public record ResponseSlots(Guid DoctorId, string Day, List<Slots> Slots);
    public record Slots(Guid Id, string Date, TimeOnly StartTime, TimeOnly EndTime);
}
