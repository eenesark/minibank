namespace MiniBank.DTOs;

/// <summary>
/// Sistemde genel olarak tanımlı olan bankacılık özelliklerinin şablonu
/// </summary>
public record FeatureCatalogItemDto(
    string Key,
    string Name,
    string Description,
    string Category,
    bool DefaultEnabled,
    string? DefaultCustomValueLabel
);

/// <summary>
/// Belirli bir müşterinin açık/kapalı özellik durumu
/// </summary>
public record CustomerFeatureDto(
    string FeatureKey,
    string Name,
    string Description,
    string Category,
    bool IsEnabled,
    string? CustomValue,
    DateTime UpdatedAt,
    string? UpdatedBy
);

/// <summary>
/// Admin'in müşteriye özellik ataması veya açıp/kapatması için gönderdiği model
/// </summary>
public record SetCustomerFeatureDto(
    string FeatureKey,
    bool IsEnabled,
    string? CustomValue = null
);

/// <summary>
/// Admin'in tek seferde birden fazla özelliği toplu güncellemesi için
/// </summary>
public record BulkUpdateCustomerFeaturesDto(
    Guid CustomerId,
    List<SetCustomerFeatureDto> Features
);

