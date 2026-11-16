using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

/// <summary>
/// MiniBank Kurumsal Akıllı Asistan API Denetleyicisi (Ugi benzeri intent & navigation)
/// </summary>
public class AssistantController : BaseApiController
{
    private readonly IAssistantService _assistantService;

    public AssistantController(IAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    /// <summary>
    /// Kullanıcının doğal dilde yazdığı bankacılık isteğini işler ve yönlendirme rotası ile kurumsal cevap döner
    /// (POST /api/Assistant/chat)
    /// </summary>
    /// <param name="dto">Kullanıcı mesajı ve opsiyonel aktif sayfa</param>
    /// <returns>Asistan yanıtı, yönlendirilecek sayfa (route), parametreler ve hızlı butonlar</returns>
    [HttpPost("chat")]
    [HttpPost("ask")]
    public async Task<ActionResult<ApiResponse<AssistantResponseDto>>> Chat([FromBody] AssistantRequestDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Message))
        {
            return ErrorBadRequest<AssistantResponseDto>("Lütfen bir mesaj veya soru iletiniz.");
        }

        var response = await _assistantService.ProcessMessageAsync(CurrentUserId, dto);
        return Success(response, "Asistan yanıtı başarıyla üretildi.");
    }
}

