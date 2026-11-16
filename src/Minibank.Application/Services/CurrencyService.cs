using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;

namespace MiniBank.Services;

public class CurrencyService : ICurrencyService
{
    private const string CacheKey = "TCMB_EXCHANGE_RATES";
    private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";
    private const decimal TroyOunceToGrams = 31.1034768m;
    private const decimal DefaultGoldOuncePriceUsd = 2720.00m; // Standart Ons Altın Gösterge Fiyatı (USD)
    private const decimal BankSpread = 0.005m; // Banka Alış/Satış Fark Marjı (%0.5)

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureManagementService _featureService;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IUnitOfWork unitOfWork,
        IFeatureManagementService featureService,
        ILogger<CurrencyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _unitOfWork = unitOfWork;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task<IEnumerable<CurrencyRateDto>> GetRatesAsync()
    {
        if (_memoryCache.TryGetValue(CacheKey, out IEnumerable<CurrencyRateDto>? cachedRates) && cachedRates is not null)
        {
            return cachedRates;
        }

        var rates = await FetchRatesFromTcmbAsync();
        _memoryCache.Set(CacheKey, rates, TimeSpan.FromMinutes(15));
        return rates;
    }

    public async Task<CurrencyRateDto?> GetRateByCodeAsync(string code)
    {
        var cleanCode = code.Trim().ToUpperInvariant();
        var rates = await GetRatesAsync();
        return rates.FirstOrDefault(r => r.Code == cleanCode);
    }

    public async Task<CurrencyCalculateResultDto> CalculateAsync(CurrencyCalculateDto dto)
    {
        var from = dto.FromCurrency.Trim().ToUpperInvariant();
        var to = dto.ToCurrency.Trim().ToUpperInvariant();

        if (from == to)
        {
            throw new InvalidOperationException("Kaynak ve hedef para birimi aynı olamaz.");
        }

        var rates = (await GetRatesAsync()).ToDictionary(r => r.Code);

        // 1. Hesaplama: TRY -> Döviz/Altın
        if (from == "TRY" && rates.TryGetValue(to, out var targetRate))
        {
            var rateUsed = targetRate.SellingRate; // Banka Satış kuru uygulanır
            decimal fromAmount, toAmount;

            if (dto.Mode.Equals("TargetAmount", StringComparison.OrdinalIgnoreCase))
            {
                toAmount = dto.Amount;
                fromAmount = Math.Round(dto.Amount * rateUsed, 2);
            }
            else
            {
                fromAmount = dto.Amount;
                toAmount = Math.Round(dto.Amount / rateUsed, 4);
            }

            return new CurrencyCalculateResultDto(
                from, fromAmount, to, toAmount, rateUsed,
                $"{fromAmount:N2} TRY ile {toAmount:N4} {to} alınabilir. (Kur: {rateUsed:N4})"
            );
        }

        // 2. Hesaplama: Döviz/Altın -> TRY
        if (to == "TRY" && rates.TryGetValue(from, out var sourceRate))
        {
            var rateUsed = sourceRate.BuyingRate; // Banka Alış kuru uygulanır
            decimal fromAmount, toAmount;

            if (dto.Mode.Equals("TargetAmount", StringComparison.OrdinalIgnoreCase))
            {
                toAmount = dto.Amount;
                fromAmount = Math.Round(dto.Amount / rateUsed, 4);
            }
            else
            {
                fromAmount = dto.Amount;
                toAmount = Math.Round(dto.Amount * rateUsed, 2);
            }

            return new CurrencyCalculateResultDto(
                from, fromAmount, to, toAmount, rateUsed,
                $"{fromAmount:N4} {from} bozdurulduğunda {toAmount:N2} TRY elde edilir. (Kur: {rateUsed:N4})"
            );
        }

        // 3. Hesaplama: Çapraz Kur (Döviz -> Döviz, örn: USD -> EUR veya USD -> XAU)
        if (rates.TryGetValue(from, out var sRate) && rates.TryGetValue(to, out var tRate))
        {
            // Kaynak dövizi önce TRY'ye çevir (BuyingRate), sonra hedef dövizi al (SellingRate)
            var tryValue = sRate.BuyingRate;
            var crossRate = Math.Round(tryValue / tRate.SellingRate, 6);

            decimal fromAmount, toAmount;
            if (dto.Mode.Equals("TargetAmount", StringComparison.OrdinalIgnoreCase))
            {
                toAmount = dto.Amount;
                fromAmount = Math.Round(dto.Amount / crossRate, 4);
            }
            else
            {
                fromAmount = dto.Amount;
                toAmount = Math.Round(dto.Amount * crossRate, 4);
            }

            return new CurrencyCalculateResultDto(
                from, fromAmount, to, toAmount, crossRate,
                $"{fromAmount:N4} {from} ile {toAmount:N4} {to} alınabilir. (Çapraz Kur: {crossRate:N4})"
            );
        }

        throw new InvalidOperationException($"Desteklenmeyen para birimi çifti: {from} -> {to}");
    }

    public async Task<ExchangeResultDto> ExchangeAsync(Guid currentUserId, ExchangeCurrencyDto dto)
    {
        // 0. Dinamik Özellik / Yetki Kontrolü: Admin bu müşteriye döviz alım/satım yetkisi vermiş mi?
        var isFeatureEnabled = await _featureService.IsFeatureEnabledAsync(currentUserId, "FX_EXCHANGE");
        if (!isFeatureEnabled)
        {
            _logger.LogWarning("Döviz/Altın işlemi reddedildi: Müşteri ({UserId}) yetkisi kapalı.", currentUserId);
            throw new UnauthorizedAccessException("Döviz ve Altın alım/satım işlemi hesabınız için yetkilendirilmemiştir. Lütfen banka yöneticiniz ile iletişime geçin.");
        }

        if (dto.Amount <= 0)
        {
            throw new InvalidOperationException("İşlem tutarı sıfırdan büyük olmalıdır.");
        }

        if (dto.SourceAccountId == dto.TargetAccountId)
        {
            throw new InvalidOperationException("Kaynak hesap ile hedef hesap aynı olamaz.");
        }

        var sourceAccount = await _unitOfWork.Accounts.GetByIdAsync(dto.SourceAccountId);
        if (sourceAccount is null)
        {
            throw new KeyNotFoundException("Kaynak banka hesabı bulunamadı.");
        }

        var targetAccount = await _unitOfWork.Accounts.GetByIdAsync(dto.TargetAccountId);
        if (targetAccount is null)
        {
            throw new KeyNotFoundException("Hedef banka hesabı bulunamadı.");
        }

        // Hesap sahipliği ve aktiflik kontrolü
        if (sourceAccount.CustomerId != currentUserId || targetAccount.CustomerId != currentUserId)
        {
            // Eğer admin değilse başkasının hesabından işlem yapamaz
            var isAdmin = await _unitOfWork.Admins.AnyAsync(a => a.Id == currentUserId);
            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("Yalnızca kendi hesaplarınız arasında döviz veya altın alım/satımı yapabilirsiniz.");
            }
        }

        if (!sourceAccount.IsActive || !targetAccount.IsActive)
        {
            throw new InvalidOperationException("Pasif durumda olan hesaplarla işlem yapılamaz.");
        }

        var fromCurrency = sourceAccount.Currency.ToUpperInvariant();
        var toCurrency = targetAccount.Currency.ToUpperInvariant();

        if (fromCurrency == toCurrency)
        {
            throw new InvalidOperationException("Kaynak ve hedef hesap aynı para birimine sahiptir. Lütfen normal transfer menüsünü kullanın.");
        }

        // Hesaplama servisini çağırarak tutarları netleştir
        var calcResult = await CalculateAsync(new CurrencyCalculateDto(
            fromCurrency,
            toCurrency,
            dto.Amount,
            dto.Mode
        ));

        var sourceDeduction = calcResult.FromAmount;
        var targetCredit = calcResult.ToAmount;

        // Bakiye kontrolü
        if (sourceAccount.Balance < sourceDeduction)
        {
            throw new InvalidOperationException($"Yetersiz bakiye! Kaynak hesapta {sourceAccount.Balance:N2} {fromCurrency} bulunmaktadır, işlem için gereken: {sourceDeduction:N2} {fromCurrency}.");
        }

        // Bakiyeleri güncelle
        sourceAccount.Balance -= sourceDeduction;
        targetAccount.Balance += targetCredit;

        // Finansal hareket kayıtları oluştur
        var sourceTx = new AccountTransaction
        {
            AccountId = sourceAccount.Id,
            Amount = sourceDeduction,
            Type = TransactionType.FxSell,
            Description = $"Döviz/Altın Alımı: {targetCredit:N2} {toCurrency} alındı. (Kur: {calcResult.RateUsed:N4})",
            TargetIban = targetAccount.Iban
        };

        var targetTx = new AccountTransaction
        {
            AccountId = targetAccount.Id,
            Amount = targetCredit,
            Type = TransactionType.FxBuy,
            Description = $"Döviz/Altın Girişi: {sourceDeduction:N2} {fromCurrency} karşılığı yüklendi.",
            TargetIban = sourceAccount.Iban
        };

        _unitOfWork.Accounts.Update(sourceAccount);
        _unitOfWork.Accounts.Update(targetAccount);
        await _unitOfWork.Transactions.AddAsync(sourceTx);
        await _unitOfWork.Transactions.AddAsync(targetTx);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Döviz/Altın işlemi tamamlandı: {UserId} - Çıkan: {Src:N2} {SrcCur} -> Giren: {Tgt:N2} {TgtCur}",
            currentUserId, sourceDeduction, fromCurrency, targetCredit, toCurrency);

        return new ExchangeResultDto(
            Success: true,
            Message: $"{sourceDeduction:N2} {fromCurrency} karşılığında {targetCredit:N2} {toCurrency} başarıyla satın alındı.",
            SourceCurrency: fromCurrency,
            SourceAmountDeducted: sourceDeduction,
            SourceNewBalance: sourceAccount.Balance,
            TargetCurrency: toCurrency,
            TargetAmountCredited: targetCredit,
            TargetNewBalance: targetAccount.Balance,
            ExchangeRateUsed: calcResult.RateUsed,
            TransactionDate: DateTime.UtcNow
        );
    }

    private async Task<List<CurrencyRateDto>> FetchRatesFromTcmbAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "MiniBank-CoreAPI/1.0");

            var response = await client.GetAsync(TcmbUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TCMB API yanıt vermedi: HTTP {StatusCode}. Güvenli varsayılan kurlar yüklenecek.", response.StatusCode);
                return GetFallbackRates();
            }

            var xmlContent = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xmlContent);

            var result = new List<CurrencyRateDto>();
            var now = DateTime.UtcNow;

            var targetCodes = new[] { "USD", "EUR", "GBP" };
            decimal usdForexBuying = 34.50m;
            decimal usdForexSelling = 34.60m;

            foreach (var code in targetCodes)
            {
                var currencyElement = doc.Descendants("Currency")
                    .FirstOrDefault(c => (string?)c.Attribute("CurrencyCode") == code);

                if (currencyElement is null) continue;

                var unitStr = (string?)currencyElement.Element("Unit") ?? "1";
                var unit = decimal.TryParse(unitStr, CultureInfo.InvariantCulture, out var u) ? u : 1m;

                var isim = (string?)currencyElement.Element("Isim") ?? code;

                var buyingStr = (string?)currencyElement.Element("ForexBuying");
                var sellingStr = (string?)currencyElement.Element("ForexSelling");

                if (decimal.TryParse(buyingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var buying) &&
                    decimal.TryParse(sellingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var selling))
                {
                    var rawBuying = Math.Round(buying / unit, 4);
                    var rawSelling = Math.Round(selling / unit, 4);

                    if (code == "USD")
                    {
                        usdForexBuying = rawBuying;
                        usdForexSelling = rawSelling;
                    }

                    // Banka Alış/Satış marjı (Spread)
                    var bankBuying = Math.Round(rawBuying * (1m - BankSpread), 4);
                    var bankSelling = Math.Round(rawSelling * (1m + BankSpread), 4);

                    result.Add(new CurrencyRateDto(
                        Code: code,
                        Name: isim,
                        BuyingRate: bankBuying,
                        SellingRate: bankSelling,
                        RawTcmbBuying: rawBuying,
                        RawTcmbSelling: rawSelling,
                        UpdatedAt: now
                    ));
                }
            }

            // Gram Altın (XAU) Hesaplaması:
            // 1 Troy Ounce = 31.1034768 gram.
            // Gram Altın Fiyatı (TL) = (Ons Fiyatı USD / 31.1034768) * USD_TRY
            var gramGoldUsd = DefaultGoldOuncePriceUsd / TroyOunceToGrams;
            var goldRawBuying = Math.Round(gramGoldUsd * usdForexBuying, 2);
            var goldRawSelling = Math.Round(gramGoldUsd * usdForexSelling, 2);

            var goldBankBuying = Math.Round(goldRawBuying * (1m - BankSpread), 2);
            var goldBankSelling = Math.Round(goldRawSelling * (1m + BankSpread), 2);

            result.Add(new CurrencyRateDto(
                Code: "XAU",
                Name: "Gram Altın (24 Ayar)",
                BuyingRate: goldBankBuying,
                SellingRate: goldBankSelling,
                RawTcmbBuying: goldRawBuying,
                RawTcmbSelling: goldRawSelling,
                UpdatedAt: now
            ));

            _logger.LogInformation("TCMB güncel kurları ve Altın fiyatı başarıyla çekildi.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCMB kurları çekilirken hata oluştu. Güvenli piyasa kurları kullanılacak.");
            return GetFallbackRates();
        }
    }

    private static List<CurrencyRateDto> GetFallbackRates()
    {
        var now = DateTime.UtcNow;
        return new List<CurrencyRateDto>
        {
            new("USD", "ABD DOLARI", 34.25m, 34.65m, 34.45m, 34.50m, now),
            new("EUR", "EURO", 37.10m, 37.55m, 37.30m, 37.38m, now),
            new("GBP", "İNGİLİZ STERLİNİ", 44.50m, 45.10m, 44.75m, 44.85m, now),
            new("XAU", "Gram Altın (24 Ayar)", 3010.50m, 3055.00m, 3030.00m, 3040.00m, now)
        };
    }
}

