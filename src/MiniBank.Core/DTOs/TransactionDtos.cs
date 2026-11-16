using MiniBank.Entities;

namespace MiniBank.DTOs;

public record DepositDto(
    Guid AccountId,
    decimal Amount,
    string Description = "Para Yatırma"
);

public record WithdrawDto(
    Guid AccountId,
    decimal Amount,
    string Description = "Para Çekme"
);

public record TransferDto(
    Guid FromAccountId,
    string TargetIban,
    decimal Amount,
    string Description = "Havale / EFT"
);

public record TransactionResponseDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string Type,
    string Description,
    string? TargetIban,
    DateTime CreatedAt,
    string Category = "Diğer"
);

