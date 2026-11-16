namespace MiniBank.DTOs;

/// <summary>
/// Anlık döviz ve altın kur bilgisi DTO
/// </summary>
public record CurrencyRateDto(
    string Code,             // Örn: "USD", "EUR", "GBP", "XAU"
    string Name,             // Örn: "ABD Doları", "Euro", "İngiliz Sterlini", "Gram Altın"
    decimal BuyingRate,      // Banka Alış (Müşterinin bankaya sattığı fiyat - TL karşılığı)
    decimal SellingRate,     // Banka Satış (Müşterinin bankadan aldığı fiyat - TL karşılığı)
    decimal RawTcmbBuying,   // TCMB Ham Alış
    decimal RawTcmbSelling,  // TCMB Ham Satış
    DateTime UpdatedAt
);

/// <summary>
/// Döviz / Altın Alım veya Satım talep DTO
/// </summary>
public record ExchangeCurrencyDto(
    Guid SourceAccountId,          // Paranın çıkacağı hesap (Örn: TRY hesabı)
    Guid TargetAccountId,          // Paranın yatacağı hesap (Örn: USD veya XAU hesabı)
    decimal Amount,                // İşlem tutarı
    string Mode = "SourceAmount"   // "SourceAmount" (Kaynak hesaptan düşecek tutar) veya "TargetAmount" (Hedef hesaba girecek tutar)
);

/// <summary>
/// Alım / Satım işlemi tamamlandığında dönen sonuç DTO
/// </summary>
public record ExchangeResultDto(
    bool Success,
    string Message,
    string SourceCurrency,
    decimal SourceAmountDeducted,
    decimal SourceNewBalance,
    string TargetCurrency,
    decimal TargetAmountCredited,
    decimal TargetNewBalance,
    decimal ExchangeRateUsed,
    DateTime TransactionDate
);

/// <summary>
/// Ön hesaplama (Döviz Çevirici / Hesap Makinesi) DTO
/// </summary>
public record CurrencyCalculateDto(
    string FromCurrency,           // Örn: "TRY"
    string ToCurrency,             // Örn: "USD" veya "XAU"
    decimal Amount,                // Örn: 1000
    string Mode = "SourceAmount"   // "SourceAmount" veya "TargetAmount"
);

/// <summary>
/// Ön hesaplama sonucu DTO
/// </summary>
public record CurrencyCalculateResultDto(
    string FromCurrency,
    decimal FromAmount,
    string ToCurrency,
    decimal ToAmount,
    decimal RateUsed,
    string Description
);

