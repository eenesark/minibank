using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;

namespace MiniBank.Services;

public class FeatureManagementService : IFeatureManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FeatureManagementService> _logger;

    // Sistemde tanımlı olan tüm bankacılık özellikleri kataloğu
    // Admin bu listeyi görür ve tek tıkla müşteriye açar/kapatır
    private static readonly List<FeatureCatalogItemDto> FeatureCatalog = new()
    {
        new(
            Key: "TIME_DEPOSIT",
            Name: "Vadeli Mevduat Hesabı",
            Description: "Müşterinin vadeli mevduat hesabı açabilmesini ve getiri elde etmesini sağlar.",
            Category: "Mevduat & Yatırım",
            DefaultEnabled: true,
            DefaultCustomValueLabel: "Özel Faiz Bonusu (%) (Örn: +2.0)"
        ),
        new(
            Key: "FX_EXCHANGE",
            Name: "Döviz ve Altın Alım-Satım",
            Description: "TCMB kurları üzerinden USD, EUR, GBP ve Gram Altın (XAU) alıp satabilme yetkisi.",
            Category: "Piyasalar",
            DefaultEnabled: true,
            DefaultCustomValueLabel: "Özel Kur İndirimi (%) (Örn: 0.2)"
        ),
        new(
            Key: "FAST_TRANSFER",
            Name: "FAST & Havale / EFT Transferleri",
            Description: "Başka IBAN'lara para transferi yapabilme yetkisi.",
            Category: "Para Transferi",
            DefaultEnabled: true,
            DefaultCustomValueLabel: "Günlük Transfer Limiti (TL) (Örn: 100000)"
        ),
        new(
            Key: "LOAN_APPLICATION",
            Name: "İhtiyaç Kredisi Başvurusu",
            Description: "Müşterinin anında hazır kredi ve taksitli nakit avans kullanabilmesi.",
            Category: "Krediler",
            DefaultEnabled: false,
            DefaultCustomValueLabel: "Maksimum Kredi Limiti (TL) (Örn: 50000)"
        ),
        new(
            Key: "CRYPTO_TRADING",
            Name: "Kripto Varlık Entegrasyonu",
            Description: "Banka güvencesiyle kripto para alım/satım ve saklama işlemleri.",
            Category: "Dijital Varlıklar",
            DefaultEnabled: false,
            DefaultCustomValueLabel: "İşlem Komisyon Oranı (%) (Örn: 0.1)"
        ),
        new(
            Key: "VIP_CUSTOMER_SUPPORT",
            Name: "VIP Öncelikli Müşteri Desteği",
            Description: "7/24 özel müşteri temsilcisi ve öncelikli işlem sırası.",
            Category: "Ayrıcalıklar",
            DefaultEnabled: false,
            DefaultCustomValueLabel: "Özel Müşteri Temsilcisi Adı"
        )
    };

    public FeatureManagementService(IUnitOfWork unitOfWork, ILogger<FeatureManagementService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public IEnumerable<FeatureCatalogItemDto> GetFeatureCatalog()
    {
        return FeatureCatalog;
    }

    public async Task<IEnumerable<CustomerFeatureDto>> GetCustomerFeaturesAsync(Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Müşteri bulunamadı.");
        }

        // Müşterinin veritabanındaki kayıtlı özellikleri
        var existingFeatures = (await _unitOfWork.CustomerFeatures.FindAsync(f => f.CustomerId == customerId))
            .ToDictionary(f => f.FeatureKey);

        var result = new List<CustomerFeatureDto>();

        // Tüm katalog elemanlarını dön, veritabanında kaydı yoksa varsayılan durumunu göster
        foreach (var catalogItem in FeatureCatalog)
        {
            if (existingFeatures.TryGetValue(catalogItem.Key, out var userFeature))
            {
                result.Add(new CustomerFeatureDto(
                    FeatureKey: catalogItem.Key,
                    Name: catalogItem.Name,
                    Description: catalogItem.Description,
                    Category: catalogItem.Category,
                    IsEnabled: userFeature.IsEnabled,
                    CustomValue: userFeature.CustomValue,
                    UpdatedAt: userFeature.UpdatedAt,
                    UpdatedBy: userFeature.UpdatedBy ?? "Admin"
                ));
            }
            else
            {
                // Henüz veritabanında özelleştirilmemişse kataloğun varsayılan değerini sun
                result.Add(new CustomerFeatureDto(
                    FeatureKey: catalogItem.Key,
                    Name: catalogItem.Name,
                    Description: catalogItem.Description,
                    Category: catalogItem.Category,
                    IsEnabled: catalogItem.DefaultEnabled,
                    CustomValue: null,
                    UpdatedAt: customer.CreatedAt,
                    UpdatedBy: "Sistem (Varsayılan)"
                ));
            }
        }

        return result;
    }

    public async Task<CustomerFeatureDto> SetCustomerFeatureAsync(Guid customerId, SetCustomerFeatureDto dto, string adminUsername)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Müşteri bulunamadı.");
        }

        var catalogItem = FeatureCatalog.FirstOrDefault(f => f.Key.Equals(dto.FeatureKey, StringComparison.OrdinalIgnoreCase));
        if (catalogItem is null)
        {
            throw new InvalidOperationException($"Geçersiz özellik anahtarı: '{dto.FeatureKey}'. Katalogda tanımlı değil.");
        }

        var existing = (await _unitOfWork.CustomerFeatures.FindAsync(f =>
            f.CustomerId == customerId && f.FeatureKey == catalogItem.Key)).FirstOrDefault();

        if (existing is null)
        {
            existing = new CustomerFeature
            {
                CustomerId = customerId,
                FeatureKey = catalogItem.Key,
                IsEnabled = dto.IsEnabled,
                CustomValue = dto.CustomValue?.Trim(),
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = adminUsername
            };
            await _unitOfWork.CustomerFeatures.AddAsync(existing);
        }
        else
        {
            existing.IsEnabled = dto.IsEnabled;
            if (dto.CustomValue is not null)
            {
                existing.CustomValue = string.IsNullOrWhiteSpace(dto.CustomValue) ? null : dto.CustomValue.Trim();
            }
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = adminUsername;
            _unitOfWork.CustomerFeatures.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin ({Admin}) müşteri ({CustomerId}) özelliğini güncelledi: {Key} -> IsEnabled: {Enabled}, Value: {Val}",
            adminUsername, customerId, catalogItem.Key, dto.IsEnabled, existing.CustomValue);

        return new CustomerFeatureDto(
            FeatureKey: catalogItem.Key,
            Name: catalogItem.Name,
            Description: catalogItem.Description,
            Category: catalogItem.Category,
            IsEnabled: existing.IsEnabled,
            CustomValue: existing.CustomValue,
            UpdatedAt: existing.UpdatedAt,
            UpdatedBy: existing.UpdatedBy
        );
    }

    public async Task<IEnumerable<CustomerFeatureDto>> BulkUpdateCustomerFeaturesAsync(Guid customerId, List<SetCustomerFeatureDto> features, string adminUsername)
    {
        var results = new List<CustomerFeatureDto>();
        foreach (var item in features)
        {
            var updated = await SetCustomerFeatureAsync(customerId, item, adminUsername);
            results.Add(updated);
        }
        return results;
    }

    public async Task<bool> IsFeatureEnabledAsync(Guid customerId, string featureKey)
    {
        var cleanKey = featureKey.Trim().ToUpperInvariant();

        var feature = (await _unitOfWork.CustomerFeatures.FindAsync(f =>
            f.CustomerId == customerId && f.FeatureKey.ToUpper() == cleanKey)).FirstOrDefault();

        if (feature is not null)
        {
            return feature.IsEnabled;
        }

        // Kayıt yoksa katalogdaki varsayılan değeri geçerlidir
        var catalogItem = FeatureCatalog.FirstOrDefault(f => f.Key.Equals(cleanKey, StringComparison.OrdinalIgnoreCase));
        return catalogItem?.DefaultEnabled ?? false;
    }

    public async Task<string?> GetCustomFeatureValueAsync(Guid customerId, string featureKey)
    {
        var cleanKey = featureKey.Trim().ToUpperInvariant();
        var feature = (await _unitOfWork.CustomerFeatures.FindAsync(f =>
            f.CustomerId == customerId && f.FeatureKey.ToUpper() == cleanKey)).FirstOrDefault();

        return feature?.CustomValue;
    }
}

