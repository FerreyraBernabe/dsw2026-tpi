using Dsw2026Tpi.Api.Configurations;
using Dsw2026Tpi.Api.Middlewares;
using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Dsw2026Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Iniciando aplicación Dsw2026Tpi.Api");

            var builder = WebApplication.CreateBuilder(args);

            builder.AddSerilogConfiguration();
            builder.Services.AddAppIdentity();
            builder.Services.AddAppAuthentication(builder.Configuration);
            builder.Services.AddSwaggerConfiguration();
            builder.Services.AddApplicationPersistence(builder.Configuration);
            builder.Services.AddAppCors(builder.Configuration);
            builder.Services.AddAppDependencies();
            builder.Services.AddCustomRateLimiting(builder.Configuration);
            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var error = new ErrorResponse(nameof(ErrorCodes.VALIDATION_ERROR), ErrorCodes.VALIDATION_ERROR);
                        var details = context.ModelState
                            .Where(entry => entry.Value?.Errors.Count > 0)
                            .SelectMany(entry => entry.Value!.Errors.Select(err => (
                                string.IsNullOrWhiteSpace(entry.Key) ? "request" : entry.Key,
                                string.IsNullOrWhiteSpace(err.ErrorMessage) ? "Formato de dato inválido." : err.ErrorMessage)));
                        error.AddDetail(details);
                        return new BadRequestObjectResult(error);
                    };
                });
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            app.UseSerilogRequestLogging();

            if (app.Environment.IsProduction())
            {
                app.UseHttpsRedirection();
            }
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                if (app.Configuration.GetValue<bool>("ResetBdOnStart"))
                {
                    app.ResetDbOnStart();
                }
                await app.SeedAdminAsync();
            }
            app.UseRouting();
            app.UseCors();
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();
            app.MapHealthChecks("/health-check");

            Log.Information("Aplicación iniciada correctamente");
            Console.WriteLine("|##################---------------###################|");
            Console.WriteLine("|                                                    |");
            Console.WriteLine("|                  API LEVANTADA                     |");
            Console.WriteLine("|                                                    |");
            Console.WriteLine("|##################---------------###################|");

            await app.RunAsync();
        }
        catch (HostAbortedException)
        {
            Log.Information("El host fue abortado (normal durante migraciones de EF Core)");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "La aplicación falló al iniciar");
            throw;
        }
        finally
        {
            Log.Information("Cerrando aplicación");
            await Log.CloseAndFlushAsync();
        }
    }
}
