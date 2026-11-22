using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Intefaces;
using Dsw2026Tpi.Application.Validation;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services
{
    public class SpecialtyService : ISpecialtyService
    {
        private readonly IPersistence _persistence;

        public SpecialtyService(IPersistence persistence)
        {
            _persistence = persistence;
        }

        public async Task<SpecialtyModel.CreateResponse> AddSpecialty(SpecialtyModel.Request request)
        {
            SpecialtyValidator.Validate(request);

            var normalizedName = request.name.Trim();
            var normalizedDescription = request.description.Trim();

            var existing = await _persistence.First<Specialty>(
                s => s.Name.ToLower() == normalizedName.ToLower());

            if (existing != null)
            {
                throw new ConflictException(
                    "DUPLICATE_ENTITY",
                    $"Ya existe una especialidad con el nombre '{normalizedName}'.");
            }

            var newSpecialty = new Specialty(normalizedName, normalizedDescription);
            await _persistence.Add(newSpecialty);
            return new SpecialtyModel.CreateResponse(newSpecialty.Id, newSpecialty.Name, newSpecialty.Description);
        }

        public async Task DeleteSpecialty(Guid id)
        {
            var specialty = await _persistence.GetById<Specialty>(id);
            if (specialty == null)
            {
                throw new EntityNotFoundException(nameof(Specialty));
            }
            specialty.Delete();
            await _persistence.Update(specialty);
        }

        public async Task<SpecialtyModel.UpdateResponse> UpdateSpecialty(Guid id, SpecialtyModel.Request request)
        {
            var specialty = await _persistence.GetById<Specialty>(id);
            if (specialty is null)
            {
                throw new EntityNotFoundException(nameof(Specialty));
            }

            SpecialtyValidator.Validate(request);

            var normalizedName = request.name.Trim();
            var normalizedDescription = request.description.Trim();

            var duplicate = await _persistence.First<Specialty>(
                s => s.Id != id && s.Name.ToLower() == normalizedName.ToLower());

            if (duplicate != null)
            {
                throw new ConflictException(
                    "DUPLICATE_ENTITY",
                    $"Ya existe otra especialidad con el nombre '{normalizedName}'.");
            }

            specialty.Update(normalizedName, normalizedDescription);
            await _persistence.Update(specialty);
            return new SpecialtyModel.UpdateResponse(specialty.Id, specialty.Name, specialty.Description);
        }

        public async Task<Pagination<SpecialtyModel.FilterResponse>> FilterSpecialtyByName(int pageSize, int pageIndex, string? name = null)
        {
            SpecialtyValidator.ValidateNameFilter(name);
            var filterName = name?.Trim();

            var specialties = await _persistence.Paginate<Specialty, string>(
                pageSize,
                pageIndex,
                s => !s.Deleted &&
                     s.IsActive &&
                     (string.IsNullOrWhiteSpace(filterName) || s.Name.Contains(filterName)),
                x => x.Name);

            return specialties.Map(s => new SpecialtyModel.FilterResponse(
                s.Id,
                s.Name,
                s.IsActive,
                s.Description));
        }

        public async Task ReactivateSpecialty(Guid id)
        {
            var specialty = await _persistence.GetById<Specialty>(id);
            if (specialty is null)
            {
                throw new EntityNotFoundException(nameof(Specialty));
            }
            specialty.Activate();
            await _persistence.Update(specialty);
        }
    }
}
