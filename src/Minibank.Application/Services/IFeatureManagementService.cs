using MiniBank.DTOs;

namespace MiniBank.Services;

public interface IFeatureManagementService
{
    /// <summary>
    /// Sistemde açılıp kapatılabilecek tüm özelliklerin şablon/katalog listesini döner
    /// </summary>
    IEnumerable<FeatureCatalogItemDto> GetFeatureCatalog();

    /// <summary>
    /// Belirtilen müşterinin tüm özellik durumlarını (açık/kapalı ve özel parametreleriyle) döner
    /// </summary>
    Task<IEnumerable<CustomerFeatureDto>> GetCustomerFeaturesAsync(Guid customerId);

    /// <summary>
    /// Admin'in müşteriye belirli bir özelliği açıp kapatmasını veya özel parametre atamasını sağlar
    /// </summary>
    Task<CustomerFeatureDto> SetCustomerFeatureAsync(Guid customerId, SetCustomerFeatureDto dto, string adminUsername);

    /// <summary>
    /// Admin'in müşteriye birden çok özelliği toplu atamasını sağlar
    /// </summary>
    Task<IEnumerable<CustomerFeatureDto>> BulkUpdateCustomerFeaturesAsync(Guid customerId, List<SetCustomerFeatureDto> features, string adminUsername);

    /// <summary>
    /// Sistem içi yetki kontrolü: Müşterinin bu özelliği kullanma izni var mı?
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(Guid customerId, string featureKey);

    /// <summary>
    /// Müşterinin özelliğe bağlı özel değerini döner (Örn: özel faiz bonusu veya limit)
    /// </summary>
    Task<string?> GetCustomFeatureValueAsync(Guid customerId, string featureKey);
}

