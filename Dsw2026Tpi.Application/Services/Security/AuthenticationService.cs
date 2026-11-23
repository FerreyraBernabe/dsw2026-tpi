using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Dsw2026Tpi.Application.Services.Security;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IPersistence _persistence;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole> roleManager,
        JwtService jwtService,
        ILogger<AuthenticationService> logger,
        IPersistence persistence)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _logger = logger;
        _persistence = persistence;
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        var errores = new List<(string, string)>();

        if (request == null)
        {
            errores.Add(("request", "Los datos de inicio de sesión son obligatorios."));
        }
        else
        {
            if (!request.Email.IsEmailValid())
            {
                errores.Add(("email", "El formato del correo electrónico no es válido."));
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                errores.Add(("password", "La contraseña es obligatoria y debe tener al menos 8 caracteres."));
            }
        }

        if (errores.Any())
        {
            throw new ValidationException(ErrorCodes.LOGIN_USER_INVALID, nameof(ErrorCodes.LOGIN_USER_INVALID))
                .WithDetail(errores);
        }

        var user = await _userManager.FindByEmailAsync(request!.Email) ?? throw new AuthenticationException();
        var result = await _signInManager.CheckPassword(user, request.Password);

        if (!result)
        {
            _logger.LogError("Intento de login fallido para: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

        if (!string.Equals(role, Roles.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Intento de login admin con rol inválido para: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var token = _jwtService.GenerateToken(user.UserName!, Roles.Administrator);

        return new LoginAdminModel.Response(
            token,
            Roles.Administrator
        );
    }

    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        if (request == null)
        {
            throw new ValidationException(ErrorCodes.LOGIN_USER_INVALID, nameof(ErrorCodes.LOGIN_USER_INVALID));
        }

        string dniStr = request.Dni.ToString();

        if (!request.Email.IsEmailValid() || !dniStr.IsDniValid())
        {
            throw new ValidationException(ErrorCodes.LOGIN_USER_INVALID,
                nameof(ErrorCodes.LOGIN_USER_INVALID));
        }

        var patient = await _persistence.First<Patient>(p => p.Dni == dniStr);

        if (patient == null)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT),
                        ErrorCodes.REGISTER_USER_CONFLICT)
                        .WithDetail(result.Errors.Select(e => (e.Code, e.Description)));
                }
                await _userManager.AddToRoleAsync(user, Roles.Patient);
            }

            await _persistence.Add(new Patient { UserId = Guid.Parse(user.Id), Email = request.Email, Dni = dniStr });
            _logger.LogInformation("Entidad paciente registrada: {Dni}", dniStr);
        }
        else
        {
            if (!string.Equals(patient.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("El Email no coincide con el DNI proporcionado: {Email}", request.Email);
                throw new AuthenticationException();
            }
        }

        var token = _jwtService.GenerateToken(dniStr, Roles.Patient);
        return new LoginPatientModel.Response(token, Roles.Patient);
    }

    public async Task<RegisterModel.Response> Register(RegisterModel.Request request)
    {
        if (!request.Email.IsEmailValid())
        {
            throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID, nameof(ErrorCodes.REGISTER_USER_INVALID));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT),
                ErrorCodes.REGISTER_USER_CONFLICT)
                    .WithDetail(result.Errors.Select(e => (e.Code, e.Description)));
        }

        _ = await _userManager.AddToRoleAsync(user, Roles.Administrator);

        _logger.LogInformation("Usuario registrado: {Email}", request.Email);

        return new RegisterModel.Response(request.Email);
    }
}
