using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Application.Validation;

public class DoctorValidator
{
    public static void Validate(DoctorModel.Request? request)
    {
        var errores = new List<(string, string)>();

        if (request == null)
        {
            errores.Add(("request", "Los datos del médico son obligatorios."));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errores.Add(("name", "El nombre no puede ser nulo o vacío."));
            }
            else if (request.Name.Trim().Length > 100 || request.Name.Trim().Length < 3)
            {
                errores.Add(("name", "La longitud del nombre debe estar entre 3 y 100 caracteres."));
            }

            if (request.SpecialtyId == Guid.Empty)
            {
                errores.Add(("specialtyId", "El ID de la especialidad no puede estar vacío."));
            }
        }

        if (errores.Any())
        {
            throw new ValidationException(
                "Se encontraron errores en los datos enviados",
                nameof(ErrorCodes.VALIDATION_ERROR)
            ).WithDetail(errores);
        }
    }

    public static void ValidateNameFilter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 100)
        {
            throw new ValidationException(
                "Se encontraron errores en los datos enviados",
                nameof(ErrorCodes.VALIDATION_ERROR)
            ).WithDetail("name", "El filtro de nombre debe tener entre 3 y 100 caracteres.");
        }
    }
}
