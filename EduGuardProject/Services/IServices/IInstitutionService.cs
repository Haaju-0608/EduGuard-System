using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface IInstitutionService
    {
        Task<(IEnumerable<InstitutionResponseDto> Items, int TotalCount)> GetInstitutionsAsync(string? search, string? sort, int page, int pageSize);
        Task<InstitutionResponseDto?> GetInstitutionByIdAsync(Guid id);
        Task<InstitutionResponseDto> CreateInstitutionAsync(CreateInstitutionDto dto);
        Task<bool> UpdateInstitutionAsync(Guid id, UpdateInstitutionDto dto);
        Task<bool> DeleteInstitutionAsync(Guid id);
    }
}
