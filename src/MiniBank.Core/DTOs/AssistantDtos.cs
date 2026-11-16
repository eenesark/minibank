namespace MiniBank.DTOs;

/// <summary>
/// Kullanıcının asistana gönderdiği soru/mesaj isteği
/// </summary>
public class AssistantRequestDto
{
    /// <summary>
    /// Kullanıcının yazdığı doğal dildeki mesaj (Örn: "Ahmet'e 500 TL göndermek istiyorum")
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Varsa kullanıcının o an bulunduğu ekran/sayfa bilgisi (Opsiyonel)
    /// </summary>
    public string? CurrentPage { get; set; }
}

/// <summary>
/// Asistanın kullanıcıya döndüğü kurumsal yanıt ve eylem paketi
/// </summary>
public class AssistantResponseDto
{
    /// <summary>
    /// Asistanın kullanıcıya hitap eden kurumsal cevabı
    /// (Örn: "Sizi hemen Para Transferi sayfasına yönlendiriyorum.")
    /// </summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>
    /// Frontend'in yapması gereken eylem türü (NAVIGATE, INFO, SUGGESTION)
    /// </summary>
    public string Action { get; set; } = "INFO";

    /// <summary>
    /// Yönlendirilecek frontend rotası (Örn: "/transfers", "/exchange", "/accounts/new")
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Rota için önerilen parametreler (Örn: tutar: 500, alıcıIBAN: "TR...")
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Kullanıcıya sunulabilecek hızlı yanıt/aksiyon öneri butonları
    /// (Örn: ["Para Gönder", "Döviz Kurları", "Yeni Hesap Aç"])
    /// </summary>
    public List<string> QuickSuggestions { get; set; } = new();
}

