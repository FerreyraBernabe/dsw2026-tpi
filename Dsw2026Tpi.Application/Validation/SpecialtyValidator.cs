using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Application.Validation
{
    public class SpecialtyValidator
    {
        public static void Validate(SpecialtyModel.Request? request)
        {
            var errores = new List<(string, string)>();

            if (request == null)
            {
                errores.Add(("request", "La especialidad no puede ser nula."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.name))
                {
                    errores.Add(("name", "La especialidad debe tener un nombre."));
                }
                else if (request.name.Trim().Length > 100 || request.name.Trim().Length < 3)
                {
                    errores.Add(("name", "El nombre de la especialidad debe estar entre los 3 y 100 caracteres."));
                }

                if (string.IsNullOrWhiteSpace(request.description))
                {
                    errores.Add(("description", "La especialidad debe tener una descripción."));
                }
                else if (request.description.Trim().Length > 100 || request.description.Trim().Length < 10)
                {
                    errores.Add(("description", "La descripción de la especialidad debe estar entre los 10 y 100 caracteres."));
                }
            }

            if (errores.Any())
            {
                throw new ValidationException("Se encontraron errores en los datos enviados", nameof(ErrorCodes.VALIDATION_ERROR))
                    .WithDetail(errores);
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
                throw new ValidationException("Se encontraron errores en los datos enviados", nameof(ErrorCodes.VALIDATION_ERROR))
                    .WithDetail("name", "El filtro de nombre debe tener entre 3 y 100 caracteres.");
            }
        }
    }
}
