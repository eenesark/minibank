using MiniBank.Entities;

namespace MiniBank.Data.Repositories;

public interface ICardRepository : IRepository<Card>
{
    Task<IEnumerable<Card>> GetAllWithDetailsAsync();
    Task<IEnumerable<Card>> GetByCustomerIdWithDetailsAsync(Guid customerId);
    Task<IEnumerable<Card>> GetByAccountIdWithDetailsAsync(Guid accountId);
    Task<Card?> GetByIdWithDetailsAsync(Guid cardId);
}
