using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public class CardRepository : Repository<Card>, ICardRepository
{
    public CardRepository(MiniBankDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Card>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.Account == null || c.Account.AccountType == "Demand")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetByCustomerIdWithDetailsAsync(Guid customerId)
    {
        return await _dbSet
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.CustomerId == customerId && (c.Account == null || c.Account.AccountType == "Demand"))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetByAccountIdWithDetailsAsync(Guid accountId)
    {
        return await _dbSet
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.AccountId == accountId && (c.Account == null || c.Account.AccountType == "Demand"))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Card?> GetByIdWithDetailsAsync(Guid cardId)
    {
        return await _dbSet
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId);
    }
}
