using MiniBank.Data.Repositories;

namespace MiniBank.Data;

public interface IUnitOfWork : IDisposable
{
    ICustomerRepository Customers { get; }
    IAccountRepository Accounts { get; }
    IAdminRepository Admins { get; }
    ITransactionRepository Transactions { get; }
    IRepository<MiniBank.Entities.CustomerFeature> CustomerFeatures { get; }
    ICardRepository Cards { get; }
    Task<int> SaveChangesAsync();
}

