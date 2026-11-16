using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class CurrencyController : BaseApiController
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    /// <summary>
    /// TCMB'den çekilen güncel döviz kurlarını ve altın fiyatlarını listeler
    /// </summary>
    [HttpGet("rates")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CurrencyRateDto>>>> GetRates()
    {
        var rates = await _currencyService.GetRatesAsync();
        return Success(rates, "TCMB güncel piyasa kurları başarıyla getirildi.");
    }

    /// <summary>
    /// Belirli bir para biriminin (USD, EUR, GBP, XAU) kurunu getirir
    /// </summary>
    [HttpGet("rates/{code}")]
    public async Task<ActionResult<ApiResponse<CurrencyRateDto>>> GetRateByCode(string code)
    {
        var rate = await _currencyService.GetRateByCodeAsync(code);
        if (rate is null)
        {
            return ErrorNotFound<CurrencyRateDto>($"'{code}' kodlu para birimi bulunamadı.");
        }

        return Success(rate, $"{code} kuru başarıyla getirildi.");
    }

    /// <summary>
    /// Döviz / Altın Çevirici (Hesap Makinesi - Önceden tutar hesaplama)
    /// </summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<ApiResponse<CurrencyCalculateResultDto>>> Calculate([FromBody] CurrencyCalculateDto dto)
    {
        try
        {
            var result = await _currencyService.CalculateAsync(dto);
            return Success(result, "Kur hesaplaması tamamlandı.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CurrencyCalculateResultDto>(ex.Message);
        }
    }

    /// <summary>
    /// Müşterinin kendi hesapları arasında Döviz veya Altın Alım / Satımı yapar
    /// </summary>
    [Authorize]
    [HttpPost("exchange")]
    public async Task<ActionResult<ApiResponse<ExchangeResultDto>>> Exchange([FromBody] ExchangeCurrencyDto dto)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<ExchangeResultDto>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        try
        {
            var result = await _currencyService.ExchangeAsync(currentUserId, dto);
            return Success(result, result.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<ExchangeResultDto>(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ExchangeResultDto>.Fail(ex.Message, 403));
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<ExchangeResultDto>(ex.Message);
        }
    }
}
