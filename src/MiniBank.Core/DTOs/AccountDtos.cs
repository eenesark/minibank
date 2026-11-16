namespace MiniBank.DTOs;

public record CreateAccountDto(
    Guid CustomerId,
    string Currency = "TRY",
    decimal InitialDeposit = 0,
    string? CustomIban = null,
    string? CustomAccountNumber = null
);

public record UpdateAccountDto(
    string? AccountNumber,
    string? Iban,
    string? Currency,
    bool? IsActive
);

public record AccountResponseDto(
    Guid Id,
    string AccountNumber,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsActive,
    Guid CustomerId,
    string CustomerFullName,
    DateTime CreatedAt,
    string AccountType = "Demand",
    int? MaturityDays = null,
    decimal? InterestRate = null,
    DateTime? MaturityDate = null,
    decimal? ExpectedInterestAmount = null
);

public record OpenCurrencyAccountDto(
    string Currency,
    decimal InitialDeposit = 0
);

