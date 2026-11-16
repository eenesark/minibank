using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

[Authorize]
public class CardsController : BaseApiController
{
    private readonly ICardService _cardService;

    public CardsController(ICardService cardService)
    {
        _cardService = cardService;
    }

    /// <summary>
    /// Giriş yapan kullanıcının tüm banka kartlarını listeler
    /// </summary>
    [HttpGet]
    [HttpGet("my-cards")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CardResponseDto>>>> GetMyCards()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return ErrorUnauthorized<IEnumerable<CardResponseDto>>("Oturum bilgisi doğrulanamadı.");
        }

        var cards = await _cardService.GetByCustomerIdAsync(userId);
        return Success(cards, "Kartlarınız başarıyla listelendi.");
    }

    /// <summary>
    /// Kart detaylarını getirir (Güvenlik: Yalnızca kart sahibi erişebilir)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CardResponseDto>>> GetById(Guid id)
    {
        TryGetCurrentUserId(out var userId);

        try
        {
            var card = await _cardService.GetByIdAsync(id, userId == Guid.Empty ? null : userId);
            if (card is null)
            {
                return ErrorNotFound<CardResponseDto>("Kart bulunamadı.");
            }
            return Success(card, "Kart detayları başarıyla getirildi.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorForbidden<CardResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Belirli bir banka hesabına ait kartları getirir
    /// </summary>
    [HttpGet("account/{accountId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CardResponseDto>>>> GetByAccountId(Guid accountId)
    {
        var cards = await _cardService.GetByAccountIdAsync(accountId);
        return Success(cards, "Hesaba bağlı kartlar başarıyla listelendi.");
    }

    /// <summary>
    /// Belirtilen hesaba yeni bir Banka Kartı (Debit) çıkartır
    /// </summary>
    [HttpPost]
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<CardResponseDto>>> Create([FromBody] CreateCardDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return ErrorUnauthorized<CardResponseDto>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var card = await _cardService.CreateCardAsync(userId, dto);
            return CreatedSuccess(card, "Yeni banka kartınız başarıyla oluşturuldu.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CardResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CardResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Kart ayarlarını günceller (Yurtdışı açık/kapalı, İnternet alışverişi, Limitler vb.)
    /// </summary>
    [HttpPut("{id:guid}/settings")]
    public async Task<ActionResult<ApiResponse<CardResponseDto>>> UpdateSettings(Guid id, [FromBody] UpdateCardSettingsDto dto)
    {
        TryGetCurrentUserId(out var userId);

        try
        {
            var updated = await _cardService.UpdateCardSettingsAsync(id, dto, userId == Guid.Empty ? null : userId);
            return Success(updated, "Kart ayarlarınız başarıyla güncellendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CardResponseDto>(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorForbidden<CardResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CardResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Kartı geçici olarak kapatır (dondurur) veya tekrar kullanıma açar
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<ApiResponse<CardResponseDto>>> ToggleStatus(Guid id)
    {
        TryGetCurrentUserId(out var userId);

        try
        {
            var updated = await _cardService.ToggleCardStatusAsync(id, userId == Guid.Empty ? null : userId);
            var statusMsg = updated.Status == "Active" ? "Kartınız kullanıma açıldı." : "Kartınız geçici olarak donduruldu/kapatıldı.";
            return Success(updated, statusMsg);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CardResponseDto>(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorForbidden<CardResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CardResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Kartı olmayan tüm hesaplara otomatik kart ataması yapar (Gerektiğinde tetiklenebilir)
    /// </summary>
    [HttpPost("sync-missing-cards")]
    public async Task<ActionResult<ApiResponse>> SyncMissingCards()
    {
        var count = await _cardService.EnsureAllAccountsHaveCardsAsync();
        return Success($"{count} adet hesaba yeni banka kartı başarıyla tanımlandı.");
    }
}

