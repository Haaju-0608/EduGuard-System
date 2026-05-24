using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories
{
    public class InstitutionRepository : IInstitutionRepository
    {
        private readonly AppDbContext _context;
        public InstitutionRepository(AppDbContext context) => _context = context;

        public async Task<(IEnumerable<Institution> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize)
        {
            var query = _context.Institutions.AsQueryable();

            // 1. Xử lý chức năng SEARCH (Theo mục 5 trang 3)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(searchLower) || x.SubDomain.ToLower().Contains(searchLower));
            }

            // Đếm tổng số phần tử trước khi phân trang để làm metadata
            var totalCount = await query.CountAsync();

            // 2. Xử lý chức năng SORT (Mẫu cơ bản: dấu trừ "-" là giảm dần)
            if (!string.IsNullOrWhiteSpace(sort))
            {
                query = sort.ToLower() switch
                {
                    "name" => query.OrderBy(x => x.Name),
                    "-name" => query.OrderByDescending(x => x.Name),
                    "createdat" => query.OrderBy(x => x.CreatedAt),
                    "-createdat" => query.OrderByDescending(x => x.CreatedAt),
                    _ => query.OrderByDescending(x => x.CreatedAt) // Mặc định sắp xếp theo tin mới nhất
                };
            }
            else
            {
                query = query.OrderByDescending(x => x.CreatedAt);
            }

            // 3. Xử lý chức năng PAGING (Mục 5 trang 3)
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Institution?> GetByIdAsync(Guid id) => await _context.Institutions.FindAsync(id);

        public async Task AddAsync(Institution institution)
        {
            await _context.Institutions.AddAsync(institution);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Institution institution)
        {
            _context.Institutions.Update(institution);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Institution institution)
        {
            // Dự án dùng Soft Delete (Xóa mềm) nên tụi mình chỉ update DeletedAt nha bạn
            institution.DeletedAt = DateTime.UtcNow;
            _context.Institutions.Update(institution);
            await _context.SaveChangesAsync();
        }
    }
}
