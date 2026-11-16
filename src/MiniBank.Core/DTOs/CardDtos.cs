namespace MiniBank.DTOs;

public record CardResponseDto(
    Guid Id,
    Guid CustomerId,
    string CustomerFullName,
    Guid AccountId,
    string AccountNumber,
    string Iban,
    string AccountIban,
    string AccountType,
    string Currency,
    decimal AccountBalance,
    string CardNumber,
    string MaskedCardNumber,
    string CardHolderName,
    int ExpiryMonth,
    int ExpiryYear,
    string ExpiryFormatted, // "05/30"
    string Cvv,
    string CardType,
    string Status,
    bool IsInternationalAllowed,
    bool IsOnlineShoppingAllowed,
    bool IsContactlessAllowed,
    decimal DailyLimit,
    decimal DailyAtmWithdrawLimit,
    DateTime CreatedAt
);

public record UpdateCardSettingsDto(
    bool? IsInternationalAllowed = null,
    bool? IsOnlineShoppingAllowed = null,
    bool? IsContactlessAllowed = null,
    decimal? DailyLimit = null,
    decimal? DailyAtmWithdrawLimit = null
);

public record CreateCardDto(
    Guid AccountId
);

