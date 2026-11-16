namespace MiniBank.DTOs;

/// <summary>
/// Vadeli mevduat faiz oran baremi DTO
/// </summary>
public record TimeDepositRateTierDto(
    string TierName,
    int MinDays,
    int? MaxDays,
    decimal InterestRate,
    string Description
);

/// <summary>
/// Vadeli mevduat getiri hesaplama isteği
/// </summary>
public record TimeDepositCalculateDto(
    decimal Amount,
    int Days
);

/// <summary>
/// Vadeli mevduat getiri hesaplama sonucu
/// </summary>
public record TimeDepositCalculateResultDto(
    decimal PrincipalAmount,
    int MaturityDays,
    decimal AnnualInterestRate,
    decimal GrossInterest,
    decimal WithholdingTax,     // Stopaj (%7.5)
    decimal NetInterest,        // Net Ele Geçecek Faiz
    decimal TotalMaturityAmount,// Vade Sonu Toplam Tutar (Anapara + Net Faiz)
    DateTime MaturityDate
);

/// <summary>
/// Yeni vadeli mevduat hesabı açma isteği
/// </summary>
public record OpenTimeDepositDto(
    Guid SourceAccountId,      // Anaparanın çekileceği vadesiz TL hesabı
    decimal Amount,            // Vadeye bağlanacak anapara tutarı
    int Days,                  // Vade gün sayısı (Örn: 32, 45, 90, 180)
    Guid? DemandAccountId = null // Frontend ile tam uyumluluk (demandAccountId)
);

/// <summary>
/// Müşterinin vadeli mevduat hesabı detay DTO
/// </summary>
public record TimeDepositAccountDto(
    Guid Id,
    string AccountNumber,
    string Iban,
    decimal PrincipalAmount,
    decimal CurrentBalance,
    decimal InterestRate,
    int MaturityDays,
    DateTime StartDate,
    DateTime MaturityDate,
    int RemainingDays,
    decimal ExpectedNetInterest,
    decimal TotalMaturityAmount,
    bool IsMatured,
    bool IsClosed,
    string Status,
    Guid? CustomerId = null
);

/// <summary>
/// Vadeli hesap kapatma veya erken vade bozma işlem sonucu DTO
/// </summary>
public record CloseTimeDepositResultDto(
    bool Success,
    string Message,
    Guid TargetAccountId,
    string TargetIban,
    decimal ReturnedPrincipal,
    decimal EarnedInterest,
    decimal TotalTransferred,
    bool WasMaturedEarly
);

