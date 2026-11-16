using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

[Authorize]
public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Giriş yapan müşterinin sadeleştirilmiş ana ekran verisini döner.
    /// Yalnızca ana vadesiz TL hesabını, özet varlığı, son işlemleri ve sol üstteki menü ağacını içerir.
    /// </summary>
    [HttpGet]
    [HttpGet("home")]
    public async Task<ActionResult<ApiResponse<CustomerDashboardDto>>> GetHomeDashboard()
    {
        if (!TryGetCurrentUserId(out var customerId))
        {
            return ErrorUnauthorized<CustomerDashboardDto>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var dashboard = await _dashboardService.GetCustomerDashboardAsync(customerId);
            return Success(dashboard, "Ana sayfa verileri başarıyla getirildi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CustomerDashboardDto>(ex.Message);
        }
    }

    /// <summary>
    /// Sol üstteki 3 çizgi (☰ hamburger) menüsü için tüm kategorileri ve alt sayfaları döner.
    /// </summary>
    [HttpGet("menu")]
    public async Task<ActionResult<ApiResponse<IEnumerable<NavigationCategoryDto>>>> GetNavigationMenu()
    {
        if (!TryGetCurrentUserId(out var customerId))
        {
            return ErrorUnauthorized<IEnumerable<NavigationCategoryDto>>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var menu = await _dashboardService.GetNavigationMenuAsync(customerId);
            return Success(menu, "Menü kategorileri başarıyla listelendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<IEnumerable<NavigationCategoryDto>>(ex.Message);
        }
    }
}

