namespace MiniBank.Entities;

public class CustomerFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Hangi müşteriye ait?
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // Özellik Anahtarı (Örn: TIME_DEPOSIT, FX_EXCHANGE, FAST_TRANSFER, LOAN_APPLICATION)
    public string FeatureKey { get; set; } = string.Empty;

    // Bu özellik açık mı?
    public bool IsEnabled { get; set; } = true;

    // İsteğe bağlı özel parametre (Örn: özel faiz oranı "+2.5", özel transfer limiti "100000")
    public string? CustomValue { get; set; }

    // Güncelleme geçmişi
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; } // Admin kullanıcı adı
}

