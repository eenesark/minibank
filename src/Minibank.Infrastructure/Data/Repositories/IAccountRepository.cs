using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public interface IAccountRepository : IRepository<Account>
{
    Task<IEnumerable<Account>> GetAccountsWithCustomerAsync();
    Task<Account?> GetAccountWithCustomerByIdAsync(Guid id);
    Task<Account?> GetAccountByIbanAsync(string iban);
    Task<Account?> GetAccountByNumberAsync(string accountNumber);
    Task<IEnumerable<Account>> GetAccountsByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<Account>> GetAccountsWithoutCardsAsync();
    Task<decimal> GetTotalBalanceAsync();
    Task<int> GetActiveAccountsCountAsync();
}

