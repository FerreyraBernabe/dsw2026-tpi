using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/doctors")]
public class DoctorController : AppController
{
    private readonly IDoctorService _service;

    public DoctorController(IDoctorService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageIndex = 1,
        [FromQuery] string? name = null,
        [FromQuery] Guid? specialtyId = null)
    {
        var doctors = await _service.GetAll(pageSize, pageIndex, name, specialtyId);
        return Ok(doctors);
    }

    [HttpGet("{id}/availabilities")]
    public async Task<IActionResult> GetAvailabilities([FromRoute] Guid id)
    {
        var rules = await _service.GetAvailabilities(id);
        return Ok(rules);
    }

    [HttpGet("{id}/slots")]
    public async Task<IActionResult> GetAvailableSlots(
        [FromRoute] Guid id,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        var slots = await _service.GetAvailableSlots(id, from, to);
        return Ok(slots);
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminPolicy)]
    public async Task<IActionResult> CreateDoctor(DoctorModel.Request request)
    {
        var doctor = await _service.CreateDoctor(request);
        return Ok(doctor);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.AdminPolicy)]
    public async Task<IActionResult> UpdateDoctor(Guid id, DoctorModel.Request request)
    {
        var doctor = await _service.UpdateDoctor(id, request);
        return Ok(doctor);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.AdminPolicy)]
    public async Task<IActionResult> DeleteDoctor([FromRoute] Guid id)
    {
        await _service.DeleteDoctor(id);
        return Ok("ok");
    }

    [HttpPut("activate")]
    [Authorize(Policy = Policies.AdminPolicy)]
    public async Task<IActionResult> ReactivateDoctor([FromQuery] Guid id)
    {
        await _service.ReactivateDoctor(id);
        return Ok("Doctor reactivado correctamente");
    }
}
