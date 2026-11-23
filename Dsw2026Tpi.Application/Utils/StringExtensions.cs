using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Application.Utils;

public static class StringExtensions
{
    public static DayOfWeek toDayOfWeek(this string? day)
    {
        return (day ?? string.Empty).Trim().ToUpper() switch
        {
            "LUNES" => DayOfWeek.Monday,
            "MARTES" => DayOfWeek.Tuesday,
            "MIÉRCOLES" or "MIERCOLES" => DayOfWeek.Wednesday,
            "JUEVES" => DayOfWeek.Thursday,
            "VIERNES" => DayOfWeek.Friday,
            "SÁBADO" or "SABADO" => DayOfWeek.Saturday,
            "DOMINGO" => DayOfWeek.Sunday,
            _ => throw new ValidationException(
                $"El día '{day}' no es válido.",
                nameof(ErrorCodes.VALIDATION_ERROR)
            ).WithDetail("day", "Debe ser un día válido de la semana (LUNES a DOMINGO).")
        };
    }

    public static string ToSpanishDayName(this DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "LUNES",
            DayOfWeek.Tuesday => "MARTES",
            DayOfWeek.Wednesday => "MIERCOLES",
            DayOfWeek.Thursday => "JUEVES",
            DayOfWeek.Friday => "VIERNES",
            DayOfWeek.Saturday => "SABADO",
            DayOfWeek.Sunday => "DOMINGO",
            _ => string.Empty
        };
    }
}
