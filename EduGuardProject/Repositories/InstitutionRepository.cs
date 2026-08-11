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
            var query = _context.Institutions
                .Where(x => x.DeletedAt == null);   // THÊM: lọc bỏ institution đã soft-delete

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(searchLower) || x.SubDomain.ToLower().Contains(searchLower));
            }

            var totalCount = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(sort))
            {
                query = sort.ToLower() switch
                {
                    "name" => query.OrderBy(x => x.Name),
                    "-name" => query.OrderByDescending(x => x.Name),
                    "createdat" => query.OrderBy(x => x.CreatedAt),
                    "-createdat" => query.OrderByDescending(x => x.CreatedAt),
                    _ => query.OrderByDescending(x => x.CreatedAt)
                };
            }
            else
            {
                query = query.OrderByDescending(x => x.CreatedAt);
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (items, totalCount);
        }

        // SỬA: FindAsync tra theo Primary Key, không filter được điều kiện WHERE -
        // đổi sang FirstOrDefaultAsync để lọc thêm DeletedAt == null (giống đúng pattern UserRepository đang làm)
        public async Task<Institution?> GetByIdAsync(Guid id) =>
            await _context.Institutions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);

        public async Task AddAsync(Institution institution)
        {
            await _context.Institutions.AddAsync(institution);
            await _context.SaveChangesAsync();
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == institution.Id);
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    Id = Guid.NewGuid(),
                    InstitutionId = institution.Id,
                    Balance = 0,
                    Currency = "VND",
                    LowBalanceThreshold = 50000,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Wallets.AddAsync(wallet);
            }
            else
            {
                wallet.LowBalanceThreshold = 50000;
                wallet.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Institution institution)
        {
            _context.Institutions.Update(institution);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Institution institution)
        {
            institution.DeletedAt = DateTime.UtcNow;
            _context.Institutions.Update(institution);
            await _context.SaveChangesAsync();
        }
    }
}