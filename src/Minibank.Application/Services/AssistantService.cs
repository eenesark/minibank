using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniBank.Data.Repositories;
using MiniBank.DTOs;

namespace MiniBank.Services;

/// <summary>
/// MiniBank Kurumsal Akıllı Asistan Servisi (Gemini LLM Entegrasyonu + Akıllı Yedek Bankacılık Motoru)
/// </summary>
public class AssistantService : IAssistantService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IHttpClientFactory httpClientFactory,
        ICustomerRepository customerRepository,
        IConfiguration configuration,
        ILogger<AssistantService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _customerRepository = customerRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AssistantResponseDto> ProcessMessageAsync(Guid? userId, AssistantRequestDto request)
    {
        var rawMessage = request?.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return new AssistantResponseDto
            {
                Reply = "Merhaba! Ben MiniBank Asistan. Para transferi, hesap açılışı veya döviz işlemlerinizde size nasıl yardımcı olabilirim?",
                Action = "INFO",
                Route = "/dashboard",
                QuickSuggestions = new() { "Para Gönder", "Döviz Kurları", "Hesaplarım", "Vadeli Mevduat" }
            };
        }

        // Giriş yapmış kullanıcının adını al (Kişiselleştirilmiş bankacılık hitabı için)
        string? customerFullName = null;
        if (userId.HasValue)
        {
            try
            {
                var customer = await _customerRepository.GetByIdAsync(userId.Value);
                if (customer != null)
                {
                    customerFullName = $"{customer.FirstName} {customer.LastName}".Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kullanıcı bilgisi alınırken hata oluştu.");
            }
        }

        // 1. Gemini API Anahtarı tanımlı ise LLM üzerinden yanıt üretmeyi dene
        var apiKey = _configuration["Assistant:GeminiApiKey"] 
                     ?? _configuration["Gemini:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.Contains("YOUR_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var llmResponse = await CallGeminiApiAsync(apiKey, rawMessage, customerFullName);
                if (llmResponse != null)
                {
                    return llmResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini API çağrısı başarısız oldu, yerel kural motoruna geçiliyor.");
            }
        }

        // 2. LLM yoksa veya hata oluştuysa yerel akıllı kural motoruyla (Fallback) cevap üret
        return ProcessWithLocalRuleEngine(rawMessage, customerFullName);
    }

    /// <summary>
    /// Google Gemini LLM API üzerinden bankacı personasıyla yanıt üretir
    /// </summary>
    private async Task<AssistantResponseDto?> CallGeminiApiAsync(string apiKey, string userMessage, string? customerName)
    {
        var model = _configuration["Assistant:Model"] ?? "gemini-1.5-flash";
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        var systemPrompt = BuildSystemPrompt(customerName);
        var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = $"{systemPrompt}\n\nKULLANICI MESAJI: \"{userMessage}\"" }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var response = await client.PostAsJsonAsync(requestUrl, requestBody);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Gemini API hata döndü. StatusCode: {Status}, Hata: {Error}", response.StatusCode, err);
            return null;
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        if (jsonDoc == null) return null;

        var root = jsonDoc.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var textContent = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent)) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var assistantResult = JsonSerializer.Deserialize<AssistantResponseDto>(textContent, options);

        if (assistantResult != null)
        {
            // Eğer parametrelerde tutar varsa ve kullanıcı mesajında yakalandıysa teyit et
            EnrichParametersWithDetectedAmount(userMessage, assistantResult);
            return assistantResult;
        }

        return null;
    }

    /// <summary>
    /// Bankacı kimliğini (Persona) belirleyen katı kurumsal talimat
    /// </summary>
    private static string BuildSystemPrompt(string? customerName)
    {
        var greeting = string.IsNullOrEmpty(customerName) ? "" : $"Müşterinin adı: '{customerName}'. Gerektiğinde 'Sayın {customerName}' diye hitap edebilirsin.";

        return $@"
SENİN KİMLİĞİN:
Sen MiniBank'ın resmi ve kurumsal dijital bankacılık asistanısın. Adın 'MiniBank Asistan'.
Kesinlikle bir yapay zeka, bot veya dil modeli olduğunu söyleme. Garanti BBVA'nın Ugi'si gibi samimi, saygılı, son derece profesyonel, kısa ve net bir bankacı üslubuyla konuş.
{greeting}

GÖREVİN:
Kullanıcının yazdığı cümleyi anla, ona 1-2 cümlelik nazik bir bankacı yanıtı ver ve onu doğru bankacılık sayfasına yönlendir.

DESTEKLENEN SAYFA ROTALARI (route):
- /transfers : Para transferi, havale, EFT, FAST, para gönderme
- /accounts : Hesaplarım, bakiyelerim, IBAN listesi
- /accounts/new : Yeni vadesiz banka hesabı açılışı
- /exchange : Döviz kurları, Dolar, Euro, Altın alım-satım
- /deposits : Vadeli mevduat, faiz oranları, birikim hesabı
- /deposits/new : Yeni vadeli hesap açma
- /transactions : Hesap hareketleri, son harcamalar, ekstre, dekont
- /profile : Müşteri profili, kişisel bilgiler
- /security : Şifre değiştirme, güvenlik ayarları
- /dashboard : Ana sayfa, hesap özeti

EYLEM TİPLERİ (action):
- NAVIGATE : Kullanıcıyı doğrudan belirtilen rotaya yönlendir.
- INFO : Sadece bilgi ver, yönlendirme yapma (rota null).

KURALLAR:
1. Asla bankacılık dışı konularda (yemek tarifi, hava durumu, şiir, kod vb.) cevap verme. 'Yalnızca MiniBank bankacılık işlemleriniz için yardımcı olabilirim.' de.
2. Cevabın en fazla 1 veya 2 kısa cümle olmalıdır.
3. Yanıtını kesinlikle aşağıdaki JSON şemasına uygun olarak üret:
{{
  ""reply"": ""Sizi hemen Para Transferi sayfasına yönlendiriyorum."",
  ""action"": ""NAVIGATE"",
  ""route"": ""/transfers"",
  ""parameters"": {{ ""amount"": 500 }},
  ""quickSuggestions"": [""Para Gönder"", ""Döviz Kurları"", ""Yeni Hesap Aç""]
}}";
    }

    /// <summary>
    /// LLM bağlantısı olmadığında anında çalışan akıllı yerel bankacılık kural motoru (Fallback)
    /// </summary>
    private AssistantResponseDto ProcessWithLocalRuleEngine(string message, string? customerName)
    {
        var norm = NormalizeText(message);
        var detectedAmount = ExtractAmount(message);
        var hitap = string.IsNullOrEmpty(customerName) ? "" : $"Sayın {customerName}, ";

        // 1. Bankacılık Dışı Konular
        if (norm.Contains("hava durumu") || norm.Contains("yemek") || norm.Contains("fikra") || 
            norm.Contains("siir") || norm.Contains("kod yaz") || norm.Contains("futbol") || norm.Contains("sarki"))
        {
            return new AssistantResponseDto
            {
                Reply = "Ben MiniBank'ın dijital asistanıyım. Yalnızca bankacılık ve finansal işlemlerinizde size yardımcı olabilirim.",
                Action = "INFO",
                Route = null,
                QuickSuggestions = new() { "Para Gönder", "Döviz Kurları", "Hesaplarım", "Vadeli Mevduat" }
            };
        }

        // 2. Para Transferi / Gönderim (Havale, EFT, FAST, Para Yolla, Gönder)
        if (norm.Contains("transfer") || norm.Contains("para gonder") || norm.Contains("para yolla") || 
            norm.Contains("havale") || norm.Contains("eft") || norm.Contains("fast") || norm.Contains("iban") ||
            norm.Contains("gonder") || norm.Contains("para at") || norm.Contains("ode") || norm.Contains("para aktar"))
        {
            var parameters = new Dictionary<string, object>();
            if (detectedAmount.HasValue)
            {
                parameters["amount"] = detectedAmount.Value;
            }

            var amountText = detectedAmount.HasValue ? $" ({detectedAmount.Value:N0} TL)" : "";
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Sizi hemen Para Transferi adımına yönlendiriyorum{amountText}.",
                Action = "NAVIGATE",
                Route = "/transfers",
                Parameters = parameters.Count > 0 ? parameters : null,
                QuickSuggestions = new() { "Hesaplarım", "Hesap Hareketleri", "Döviz Al/Sat" }
            };
        }

        // 3. Döviz ve Altın İşlemleri (Dolar, Euro, Kur)
        if (norm.Contains("doviz") || norm.Contains("dolar") || norm.Contains("euro") || 
            norm.Contains("altin") || norm.Contains("kur") || norm.Contains("sterlin") || norm.Contains("exchange"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Güncel döviz ve altın kurlarını inceleyip işlem yapabileceğiniz sayfayı açıyorum.",
                Action = "NAVIGATE",
                Route = "/exchange",
                QuickSuggestions = new() { "Dolar Al", "Euro Al", "Altın Al", "Hesaplarım" }
            };
        }

        // 4. Vadeli Mevduat & Faiz Getirisi
        if (norm.Contains("vadeli") || norm.Contains("mevduat") || norm.Contains("faiz") || 
            norm.Contains("birikim") || norm.Contains("getiri"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Vadeli mevduat faiz oranlarımızı inceleyip hesap açabileceğiniz sayfaya aktarıyorum.",
                Action = "NAVIGATE",
                Route = "/deposits",
                QuickSuggestions = new() { "Vadeli Hesap Aç", "Faiz Oranları", "Hesaplarım" }
            };
        }

        // 5. Yeni Hesap Açılışı
        if (norm.Contains("hesap ac") || norm.Contains("yeni hesap") || norm.Contains("iban al") || 
            norm.Contains("hesap olustur") || norm.Contains("vadesiz"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Yeni bir vadesiz banka hesabı açılış ekranına yönlendiriyorum.",
                Action = "NAVIGATE",
                Route = "/accounts/new",
                QuickSuggestions = new() { "Hesaplarım", "Vadeli Mevduat", "Para Transferi" }
            };
        }

        // 6. Hesaplar ve Bakiye Bilgisi
        if (norm.Contains("hesaplar") || norm.Contains("hesabim") || norm.Contains("bakiye") || 
            norm.Contains("param ne kadar") || norm.Contains("param") || norm.Contains("cuzdan"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Mevcut hesaplarınızı ve bakiyelerinizi görüntüleyebileceğiniz sayfayı açıyorum.",
                Action = "NAVIGATE",
                Route = "/accounts",
                QuickSuggestions = new() { "Para Gönder", "Yeni Hesap Aç", "Hesap Hareketleri" }
            };
        }

        // 7. Hesap Hareketleri / Ekstre / Geçmiş İşlemler
        if (norm.Contains("hareket") || norm.Contains("ekstre") || norm.Contains("dekont") || 
            norm.Contains("harcama") || norm.Contains("gecmis") || norm.Contains("son islem"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Hesap hareketlerinizi ve geçmiş işlemlerinizi listeliyorum.",
                Action = "NAVIGATE",
                Route = "/transactions",
                QuickSuggestions = new() { "Para Gönder", "Hesaplarım", "Döviz Kurları" }
            };
        }

        // 8. Güvenlik, Şifre ve Profil
        if (norm.Contains("sifre") || norm.Contains("parola") || norm.Contains("guvenlik") || 
            norm.Contains("profil") || norm.Contains("telefon") || norm.Contains("adres"))
        {
            return new AssistantResponseDto
            {
                Reply = $"{hitap}Güvenlik ve profil ayarlarınızı yönetebileceğiniz ekrana yönlendiriyorum.",
                Action = "NAVIGATE",
                Route = "/security",
                QuickSuggestions = new() { "Şifre Değiştir", "Profilim", "Hesaplarım" }
            };
        }

        // 9. Selamlama / Karşılama
        if (norm.Contains("merhaba") || norm.Contains("selam") || norm.Contains("gunaydin") || 
            norm.Contains("iyi gunler") || norm.Contains("iyi aksamlar") || norm.Contains("hey") || norm.Length < 4)
        {
            return new AssistantResponseDto
            {
                Reply = $"Merhaba {customerName ?? ""}! Ben MiniBank Asistan. Para transferi, döviz kurları veya hesap işlemlerinizde size nasıl yardımcı olabilirim?",
                Action = "INFO",
                Route = "/dashboard",
                QuickSuggestions = new() { "Para Gönder", "Döviz Kurları", "Hesaplarım", "Vadeli Mevduat" }
            };
        }

        // 10. Genel Bankacılık Yönlendirmesi
        return new AssistantResponseDto
        {
            Reply = $"{hitap}İşleminizi daha kolay yapabilmeniz için sizi Ana Sayfa ve hesap özetinize aktarıyorum. Aşağıdaki hızlı butonları da kullanabilirsiniz.",
            Action = "NAVIGATE",
            Route = "/dashboard",
            QuickSuggestions = new() { "Para Gönder", "Döviz Kurları", "Hesaplarım", "Vadeli Mevduat" }
        };
    }

    /// <summary>
    /// Türkçe ve İngilizce klavye farklarını tolere etmek için metni normalize eder
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lower = text.ToLower(new CultureInfo("tr-TR")).Trim();
        return lower
            .Replace('ç', 'c')
            .Replace('ğ', 'g')
            .Replace('ı', 'i')
            .Replace('ö', 'o')
            .Replace('ş', 's')
            .Replace('ü', 'u');
    }

    /// <summary>
    /// Metinden tutar miktarını (Örn: "500 tl", "1250 lira", "100.50") yakalar
    /// </summary>
    private static decimal? ExtractAmount(string text)
    {
        var match = Regex.Match(text, @"(?:\b)(\d{1,7}(?:[.,]\d{1,2})?)\s*(?:tl|lira|₺|usd|eur|dolar|euro)?(?:\b)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var rawNum = match.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(rawNum, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            {
                return amount;
            }
        }
        return null;
    }

    /// <summary>
    /// LLM yanıtındaki parametrelere tutar eksikse regex ile tespit edilen tutarı ekler
    /// </summary>
    private static void EnrichParametersWithDetectedAmount(string userMessage, AssistantResponseDto response)
    {
        if (response.Route == "/transfers" || response.Route == "/transfers/send")
        {
            var amount = ExtractAmount(userMessage);
            if (amount.HasValue)
            {
                response.Parameters ??= new Dictionary<string, object>();
                if (!response.Parameters.ContainsKey("amount"))
                {
                    response.Parameters["amount"] = amount.Value;
                }
            }
        }
    }
}

