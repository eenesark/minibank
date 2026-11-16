using MiniBank.DTOs;

namespace MiniBank.Services;

public interface ICardService
{
    Task<IEnumerable<CardResponseDto>> GetAllAsync();
    Task<IEnumerable<CardResponseDto>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<CardResponseDto>> GetByAccountIdAsync(Guid accountId);
    Task<CardResponseDto?> GetByIdAsync(Guid cardId, Guid? requestingCustomerId = null);
    Task<CardResponseDto> CreateCardAsync(Guid customerId, CreateCardDto dto);
    Task<CardResponseDto> UpdateCardSettingsAsync(Guid cardId, UpdateCardSettingsDto dto, Guid? requestingCustomerId = null);
    Task<CardResponseDto> ToggleCardStatusAsync(Guid cardId, Guid? requestingCustomerId = null);
    Task<int> EnsureAllAccountsHaveCardsAsync();
}

