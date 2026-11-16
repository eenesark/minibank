using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public interface IAdminRepository : IRepository<AdminUser>
{
    Task<AdminUser?> GetByUsernameOrEmailAsync(string input);
    Task<AdminUser?> GetByUsernameAsync(string username);
    Task<AdminUser?> GetByEmailAsync(string email);
    Task<AdminUser?> GetFirstAdminAsync();
    Task<IEnumerable<AdminUser>> GetAllAdminsAsync();
}

