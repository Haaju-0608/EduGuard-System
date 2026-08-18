using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface IContactRequestService
    {
        Task<(IEnumerable<ContactRequestResponseDto> Items, int TotalCount)> GetAllAsync(
            string? search, string? sort, int page, int pageSize, string? status = null);

        Task<ContactRequestResponseDto?> GetByIdAsync(Guid id);

        Task<bool> UpdateStatusAsync(Guid id, UpdateContactRequestStatusDto dto);
    }
}
