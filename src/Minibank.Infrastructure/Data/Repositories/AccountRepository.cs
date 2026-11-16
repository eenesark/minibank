using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public class AccountRepository : Repository<Account>, IAccountRepository
{
    public AccountRepository(MiniBankDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Account>> GetAccountsWithCustomerAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Customer)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Account?> GetAccountWithCustomerByIdAsync(Guid id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Account?> GetAccountByIbanAsync(string iban)
    {
        var clean = iban.Trim().ToUpperInvariant();
        return await _dbSet
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Iban == clean);
    }

    public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
    {
        var clean = accountNumber.Trim();
        return await _dbSet.FirstOrDefaultAsync(a => a.AccountNumber == clean);
    }

    public async Task<IEnumerable<Account>> GetAccountsByCustomerIdAsync(Guid customerId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Customer)
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Account>> GetAccountsWithoutCardsAsync()
    {
        return await _dbSet
            .Include(a => a.Customer)
            .Include(a => a.Cards)
            .Where(a => a.IsActive && !a.Cards.Any())
            .ToListAsync();
    }

    public async Task<decimal> GetTotalBalanceAsync()
    {
        return await _dbSet.SumAsync(a => a.Balance);
    }

    public async Task<int> GetActiveAccountsCountAsync()
    {
        return await _dbSet.CountAsync(a => a.IsActive);
    }
}

