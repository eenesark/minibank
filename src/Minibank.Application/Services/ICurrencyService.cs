using MiniBank.DTOs;

namespace MiniBank.Services;

public interface ICurrencyService
{
    /// <summary>
    /// TCMB'den güncel döviz kurlarını ve altın fiyatını çeker (Önbellekli)
    /// </summary>
    Task<IEnumerable<CurrencyRateDto>> GetRatesAsync();

    /// <summary>
    /// Belirli bir para biriminin (USD, EUR, GBP, XAU) kurunu getirir
    /// </summary>
    Task<CurrencyRateDto?> GetRateByCodeAsync(string code);

    /// <summary>
    /// İki para birimi arasında alım/satım simülasyonu (hesap makinesi) yapar
    /// </summary>
    Task<CurrencyCalculateResultDto> CalculateAsync(CurrencyCalculateDto dto);

    /// <summary>
    /// Müşterinin hesapları arasında döviz veya altın alım/satım işlemini gerçekleştirir
    /// </summary>
    Task<ExchangeResultDto> ExchangeAsync(Guid currentUserId, ExchangeCurrencyDto dto);
}

