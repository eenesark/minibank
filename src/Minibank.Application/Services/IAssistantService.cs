namespace MiniBank.Services;

using MiniBank.DTOs;

/// <summary>
/// MiniBank Akıllı Asistan Servis Arayüzü (AI & Kural Tabanlı Yönlendirme Motoru)
/// </summary>
public interface IAssistantService
{
    /// <summary>
    /// Kullanıcının doğal dilde yazdığı mesajı analiz eder, bankacı personasıyla yanıt ve yönlendirme rotası döner
    /// </summary>
    /// <param name="userId">Giriş yapmış kullanıcının Guid Id'si (oturum yoksa null)</param>
    /// <param name="request">Kullanıcının mesajı ve ekran bağlamı</param>
    /// <returns>Asistan yanıtı, eylem tipi, yönlendirilecek rota ve parametreler</returns>
    Task<AssistantResponseDto> ProcessMessageAsync(Guid? userId, AssistantRequestDto request);
}

