using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class TimeDepositController : BaseApiController
{
    private readonly ITimeDepositService _timeDepositService;

    public TimeDepositController(ITimeDepositService timeDepositService)
    {
        _timeDepositService = timeDepositService;
    }

    /// <summary>
    /// Vadeli mevduat faiz oran baremlerini listeler
    /// (1-32 Gün: %32, 33-90 Gün: %38, 91+ Gün: %41)
    /// </summary>
    [HttpGet("rates")]
    public ActionResult<ApiResponse<IEnumerable<TimeDepositRateTierDto>>> GetRates()
    {
        var tiers = _timeDepositService.GetRateTiers();
        return Success(tiers, "Vadeli mevduat faiz baremleri listelendi.");
    }

    /// <summary>
    /// Vadeli Mevduat Getiri Hesap Makinesi (Önceden brüt/net getiri hesaplama)
    /// </summary>
    [HttpPost("calculate")]
    public ActionResult<ApiResponse<TimeDepositCalculateResultDto>> Calculate([FromBody] TimeDepositCalculateDto dto)
    {
        try
        {
            var result = _timeDepositService.Calculate(dto);
            return Success(result, "Mevduat getiri hesabı tamamlandı.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<TimeDepositCalculateResultDto>(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan müşterinin vadesiz TL hesabından yeni bir vadeli mevduat hesabı açar
    /// </summary>
    [Authorize]
    [HttpPost("open")]
    public async Task<ActionResult<ApiResponse<TimeDepositAccountDto>>> OpenTimeDeposit([FromBody] OpenTimeDepositDto dto)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<TimeDepositAccountDto>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        try
        {
            var account = await _timeDepositService.OpenTimeDepositAsync(currentUserId, dto);
            return CreatedSuccess(account, "Vadeli mevduat hesabı başarıyla açıldı.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<TimeDepositAccountDto>(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TimeDepositAccountDto>.Fail(ex.Message, 403));
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<TimeDepositAccountDto>(ex.Message);
        }
    }

    /// <summary>
    /// Vadeli mevduat hesabını kapatır veya vadesinden önce bozar
    /// (Vade dolduysa faiziyle birlikte, erken bozulduysa sadece anaparayı vadesiz hesaba aktarır)
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ApiResponse<CloseTimeDepositResultDto>>> CloseTimeDeposit(Guid id, [FromQuery] Guid? targetDemandAccountId = null)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<CloseTimeDepositResultDto>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        try
        {
            var result = await _timeDepositService.CloseTimeDepositAsync(currentUserId, id, targetDemandAccountId);
            return Success(result, result.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CloseTimeDepositResultDto>(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<CloseTimeDepositResultDto>.Fail(ex.Message, 403));
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CloseTimeDepositResultDto>(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan müşterinin tüm vadeli mevduat hesaplarını (kalan gün, vade tarihi ve getiri ile) listeler
    /// </summary>
    [Authorize]
    [HttpGet("my-accounts")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TimeDepositAccountDto>>>> GetMyTimeDeposits()
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<IEnumerable<TimeDepositAccountDto>>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        var accounts = await _timeDepositService.GetMyTimeDepositsAsync(currentUserId);
        return Success(accounts, "Vadeli mevduat hesaplarınız listelendi.");
    }
}
