using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class ContactRequestService : IContactRequestService
    {
        private readonly AppDbContext _context;
        private readonly IRealtimeEventDispatcher _realtime;

        private static readonly string[] AllowedStatuses = { "PENDING", "CONTACTED", "APPROVED", "REJECTED" };

        public ContactRequestService(AppDbContext context, IRealtimeEventDispatcher realtime)
        {
            _context = context;
            _realtime = realtime;
        }

        public async Task<(IEnumerable<ContactRequestResponseDto> Items, int TotalCount)> GetAllAsync(
            string? search, string? sort, int page, int pageSize, string? status = null)
        {
            var query = _context.ContactRequests.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(c => c.Status == status.ToUpper());

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(c =>
                    c.SchoolName.ToLower().Contains(s) ||
                    c.ContactPersonName.ToLower().Contains(s) ||
                    c.Email.ToLower().Contains(s));
            }

            var totalCount = await query.CountAsync();

            query = (sort ?? "-createdAt").ToLower() switch
            {
                "createdat" => query.OrderBy(c => c.CreatedAt),
                "-createdat" => query.OrderByDescending(c => c.CreatedAt),
                "status" => query.OrderBy(c => c.Status),
                "-status" => query.OrderByDescending(c => c.Status),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ContactRequestResponseDto
                {
                    Id = c.Id,
                    SchoolName = c.SchoolName,
                    ContactPersonName = c.ContactPersonName,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    Message = c.Message,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<ContactRequestResponseDto?> GetByIdAsync(Guid id)
        {
            var entity = await _context.ContactRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return null;

            return new ContactRequestResponseDto
            {
                Id = entity.Id,
                SchoolName = entity.SchoolName,
                ContactPersonName = entity.ContactPersonName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                Message = entity.Message,
                Status = entity.Status,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<bool> UpdateStatusAsync(Guid id, UpdateContactRequestStatusDto dto)
        {
            var entity = await _context.ContactRequests.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return false;

            var newStatus = dto.Status?.Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(newStatus) || !AllowedStatuses.Contains(newStatus))
                throw new InvalidOperationException(
                    $"Invalid status. Allowed values: {string.Join(", ", AllowedStatuses)}.");

            entity.Status = newStatus;
            await _context.SaveChangesAsync();

            await _realtime.PublishDataChangedAsync(
                "contact-requests",
                "updated",
                data: new { entity.Id, entity.SchoolName, entity.Status });

            return true;
        }
    }
}