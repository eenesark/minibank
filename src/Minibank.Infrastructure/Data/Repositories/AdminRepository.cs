using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public class AdminRepository : Repository<AdminUser>, IAdminRepository
{
    public AdminRepository(MiniBankDbContext context) : base(context)
    {
    }

    public async Task<AdminUser?> GetByUsernameOrEmailAsync(string input)
    {
        var clean = input.Trim().ToLowerInvariant();
        return await _dbSet.FirstOrDefaultAsync(a =>
            a.Username.ToLower() == clean ||
            a.Email.ToLower() == clean);
    }

    public async Task<AdminUser?> GetByUsernameAsync(string username)
    {
        var clean = username.Trim().ToLowerInvariant();
        return await _dbSet.FirstOrDefaultAsync(a => a.Username.ToLower() == clean);
    }

    public async Task<AdminUser?> GetByEmailAsync(string email)
    {
        var clean = email.Trim().ToLowerInvariant();
        return await _dbSet.FirstOrDefaultAsync(a => a.Email.ToLower() == clean);
    }

    public async Task<AdminUser?> GetFirstAdminAsync()
    {
        return await _dbSet.AsNoTracking().OrderBy(a => a.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AdminUser>> GetAllAdminsAsync()
    {
        return await _dbSet.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync();
    }
}

