using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(MiniBankDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Customer>> GetCustomersWithAccountsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.Role == "Customer")
            .Include(c => c.Accounts)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Customer?> GetCustomerWithAccountsByIdAsync(Guid id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == id && c.Role == "Customer");
    }

    public async Task<Customer?> GetCustomerWithAccountsTrackingByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer?> GetByIdentityNumberOrEmailAsync(string input)
    {
        var cleanInput = input.Trim().ToLowerInvariant();
        return await _dbSet.FirstOrDefaultAsync(c =>
            c.IdentityNumber.ToLower() == cleanInput ||
            c.Email.ToLower() == cleanInput ||
            c.CustomerNumber.ToLower() == cleanInput);
    }

    public async Task<Customer?> GetByIdentityNumberAsync(string identityNumber)
    {
        var clean = identityNumber.Trim();
        return await _dbSet.FirstOrDefaultAsync(c => c.IdentityNumber == clean);
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        var clean = email.Trim().ToLowerInvariant();
        return await _dbSet.FirstOrDefaultAsync(c => c.Email.ToLower() == clean);
    }
}

