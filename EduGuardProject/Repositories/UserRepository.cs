using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> CreateUserAsync(User user)
        {
            //Mọe tui quên mất supabase tự tạo trigger nên phải update trigger đó
            var existingUser = await _context.Users.FindAsync(user.Id);

            if (existingUser != null)
            {
                existingUser.FullName = user.FullName;
                existingUser.Phone = user.Phone;
                existingUser.InstitutionId = user.InstitutionId;
                existingUser.StudentCode = user.StudentCode;

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();
                return existingUser;
            }
            else
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return user;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }


        public async Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(
    Guid? institutionId, AppRole? excludeRole, string? search, string? sort, int page, int pageSize)
        {
            var query = _context.Users.AsQueryable();
            query = query.Where(u => u.DeletedAt == null);

            // Tenant isolation — null = SuperAdmin (xem hết), có giá trị = ép theo trường
            if (institutionId.HasValue)
                query = query.Where(u => u.InstitutionId == institutionId.Value);

            // Lecturer không được thấy School Admin trong danh sách
            if (excludeRole.HasValue)
                query = query.Where(u => u.Role != excludeRole.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(x =>
                    x.FullName.ToLower().Contains(searchLower) ||
                    x.Email.ToLower().Contains(searchLower) ||
                    (x.StudentCode != null && x.StudentCode.ToLower().Contains(searchLower)));
            }

            var totalCount = await query.CountAsync();

            query = !string.IsNullOrWhiteSpace(sort)
                ? sort.ToLower() switch
                {
                    "fullname" => query.OrderBy(x => x.FullName),
                    "-fullname" => query.OrderByDescending(x => x.FullName),
                    "email" => query.OrderBy(x => x.Email),
                    "-email" => query.OrderByDescending(x => x.Email),
                    _ => query.OrderByDescending(x => x.CreatedAt)
                }
                : query.OrderByDescending(x => x.CreatedAt);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<User?> GetByIdAsync(Guid id) => await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(User user)
        {
            user.DeletedAt = DateTime.UtcNow; // Soft Delete
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }
}