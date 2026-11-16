using MiniBank.Data.Repositories;

namespace MiniBank.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly MiniBankDbContext _context;
    private ICustomerRepository? _customers;
    private IAccountRepository? _accounts;
    private IAdminRepository? _admins;
    private ITransactionRepository? _transactions;
    private IRepository<MiniBank.Entities.CustomerFeature>? _customerFeatures;
    private ICardRepository? _cards;

    public UnitOfWork(MiniBankDbContext context)
    {
        _context = context;
    }

    public ICustomerRepository Customers => _customers ??= new CustomerRepository(_context);
    public IAccountRepository Accounts => _accounts ??= new AccountRepository(_context);
    public IAdminRepository Admins => _admins ??= new AdminRepository(_context);
    public ITransactionRepository Transactions => _transactions ??= new TransactionRepository(_context);
    public IRepository<MiniBank.Entities.CustomerFeature> CustomerFeatures => _customerFeatures ??= new Repository<MiniBank.Entities.CustomerFeature>(_context);
    public ICardRepository Cards => _cards ??= new CardRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

