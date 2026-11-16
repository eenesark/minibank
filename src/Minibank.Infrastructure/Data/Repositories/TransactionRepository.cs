using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public class TransactionRepository : Repository<AccountTransaction>, ITransactionRepository
{
    public TransactionRepository(MiniBankDbContext context) : base(context)
    {
    }

    public async Task AddRangeAsync(IEnumerable<AccountTransaction> transactions)
    {
        await _dbSet.AddRangeAsync(transactions);
    }

    public async Task<IEnumerable<AccountTransaction>> GetTransactionsByAccountIdAsync(Guid accountId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountTransaction>> GetTransactionsByAccountIbanAsync(string iban)
    {
        var clean = iban.Trim().ToUpperInvariant();
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.Account)
            .Where(t => t.Account.Iban == clean || t.TargetIban == clean)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}

