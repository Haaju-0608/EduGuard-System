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
    }
}