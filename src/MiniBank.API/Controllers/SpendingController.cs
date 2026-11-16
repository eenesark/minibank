using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

[Authorize]
public class SpendingController : BaseApiController
{
    private readonly ISpendingService _spendingService;

    public SpendingController(ISpendingService spendingService)
    {
        _spendingService = spendingService;
    }

    /// <summary>
    /// Belirtilen ay ve yıl için harcama analizi, pasta grafik kategori dağılımı, gelir-gider ve bütçe durumunu döner.
    /// Parametre verilmezse içinde bulunulan ayı baz alır.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<SpendingSummaryDto>>> GetMonthlySummary([FromQuery] int? month = null, [FromQuery] int? year = null)
    {
        if (!TryGetCurrentUserId(out var customerId))
        {
            return ErrorUnauthorized<SpendingSummaryDto>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var summary = await _spendingService.GetMonthlySummaryAsync(customerId, month, year);
            return Success(summary, $"{summary.PeriodName} dönemi harcama ve bütçe özeti başarıyla getirildi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<SpendingSummaryDto>(ex.Message);
        }
    }

    /// <summary>
    /// Seçilen bir kategoriye (Örn: "Market & Gıda", "Restoran & Kafe") ait harcama detaylarını listeler.
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransactionResponseDto>>>> GetCategoryTransactions(
        string category,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null)
    {
        if (!TryGetCurrentUserId(out var customerId))
        {
            return ErrorUnauthorized<IEnumerable<TransactionResponseDto>>("Oturum bilgisi doğrulanamadı.");
        }

        var decodedCategory = Uri.UnescapeDataString(category);
        var transactions = await _spendingService.GetCategoryTransactionsAsync(customerId, decodedCategory, month, year);
        return Success(transactions, $"{decodedCategory} kategorisine ait harcamalar listelendi.");
    }

    /// <summary>
    /// Bir kategori için aylık bütçe hedefi tanımlar (Örn: Restoran bütçesi 3.000 TL)
    /// </summary>
    [HttpPost("budgets")]
    public async Task<ActionResult<ApiResponse<SpendingBudgetStatusDto>>> SetCategoryBudget([FromBody] SetCategoryBudgetDto dto)
    {
        if (!TryGetCurrentUserId(out var customerId))
        {
            return ErrorUnauthorized<SpendingBudgetStatusDto>("Oturum bilgisi doğrulanamadı.");
        }

        var result = await _spendingService.SetCategoryBudgetAsync(customerId, dto);
        return Success(result, $"{dto.Category} kategorisi için bütçe hedefi başarıyla güncellendi.");
    }
}

