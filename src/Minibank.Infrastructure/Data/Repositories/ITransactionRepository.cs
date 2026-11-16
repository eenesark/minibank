using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public interface ITransactionRepository : IReadOnlyRepository<AccountTransaction>
{
    Task AddAsync(AccountTransaction transaction);
    Task AddRangeAsync(IEnumerable<AccountTransaction> transactions);
    Task<IEnumerable<AccountTransaction>> GetTransactionsByAccountIdAsync(Guid accountId);
    Task<IEnumerable<AccountTransaction>> GetTransactionsByAccountIbanAsync(string iban);
}

