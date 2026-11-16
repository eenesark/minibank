using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<IEnumerable<Customer>> GetCustomersWithAccountsAsync();
    Task<Customer?> GetCustomerWithAccountsByIdAsync(Guid id);
    Task<Customer?> GetCustomerWithAccountsTrackingByIdAsync(Guid id);
    Task<Customer?> GetByIdentityNumberOrEmailAsync(string input);
    Task<Customer?> GetByIdentityNumberAsync(string identityNumber);
    Task<Customer?> GetByEmailAsync(string email);
}

