using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class AdminController : BaseApiController
{
    private readonly IAdminService _adminService;
    private readonly IFeatureManagementService _featureService;

    public AdminController(
        IAdminService adminService,
        IFeatureManagementService featureService)
    {
        _adminService = adminService;
        _featureService = featureService;
    }

    /// <summary>
    /// Sistemde kayıtlı tüm yöneticileri listeler (GET /api/Admin)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminResponseDto>>>> GetAll()
    {
        var admins = await _adminService.GetAllAdminsAsync();
        return Success(admins, "Yöneticiler başarıyla listelendi.");
    }

    /// <summary>
    /// Eski list bağlantısı için uyumluluk (GET /api/Admin/list)
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminResponseDto>>>> GetList()
    {
        return await GetAll();
    }

    /// <summary>
    /// Banka genel özetini ve metriklerini getirir (Toplam müşteri, bakiye, hesap sayısı vb.)
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> GetDashboard()
    {
        var dashboard = await _adminService.GetDashboardAsync();
        return Success(dashboard, "Banka yönetim paneli verileri başarıyla getirildi.");
    }

    /// <summary>
    /// Yönetici profil bilgilerini getirir.
    /// Token varsa o yöneticinin, token yoksa sistemdeki varsayılan yöneticinin bilgilerini döner.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<AdminResponseDto>>> GetMe()
    {
        var admin = await _adminService.GetMeAsync(CurrentUserId);
        if (admin is null)
        {
            return ErrorNotFound<AdminResponseDto>("Sistemde kayıtlı bir yönetici bulunamadı.");
        }

        return Success(admin, "Yönetici profil bilgileri getirildi.");
    }

    /// <summary>
    /// ID'ye göre yönetici detayını getirir (GET /api/Admin/{id})
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminResponseDto>>> GetById(Guid id)
    {
        var admin = await _adminService.GetAdminByIdAsync(id);
        if (admin is null)
        {
            return ErrorNotFound<AdminResponseDto>("Yönetici bulunamadı.");
        }

        return Success(admin, "Yönetici detayları getirildi.");
    }

    /// <summary>
    /// Giriş yapan yöneticinin bilgilerini ve şifresini günceller (Mevcut şifre doğrulamalı)
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<AdminResponseDto>>> UpdateMe([FromBody] UpdateAdminDto dto)
    {
        try
        {
            var updated = await _adminService.UpdateAdminAsync(CurrentUserId, dto);
            return Success(updated, "Yönetici profili başarıyla güncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<AdminResponseDto>(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<AdminResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Yeni bir yönetici hesabı oluşturur (POST /api/Admin)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AdminResponseDto>>> CreateAdmin([FromBody] CreateAdminDto dto)
    {
        try
        {
            var created = await _adminService.CreateAdminAsync(dto);
            return CreatedSuccess(created, "Yeni yönetici başarıyla oluşturuldu.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<AdminResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Sistemde tanımlı tüm açılabilir/kapatılabilir bankacılık özelliklerinin kataloğunu döner
    /// (Vadeli Mevduat, Döviz/Altın, FAST Transfer, Kredi vb.)
    /// </summary>
    [HttpGet("features/catalog")]
    public ActionResult<ApiResponse<IEnumerable<FeatureCatalogItemDto>>> GetFeatureCatalog()
    {
        var catalog = _featureService.GetFeatureCatalog();
        return Success(catalog, "Özellik kataloğu başarıyla getirildi.");
    }

    /// <summary>
    /// Belirtilen müşterinin tüm özellik durumlarını (Açık/Kapalı, Özel Değer) listeler
    /// </summary>
    [HttpGet("customers/{customerId:guid}/features")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerFeatureDto>>>> GetCustomerFeatures(Guid customerId)
    {
        try
        {
            var features = await _featureService.GetCustomerFeaturesAsync(customerId);
            return Success(features, "Müşteri özellikleri başarıyla listelendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<IEnumerable<CustomerFeatureDto>>(ex.Message);
        }
    }

    /// <summary>
    /// Admin'in müşteriye belirli bir özelliği açıp kapatmasını veya özel parametre (faiz/limit) atamasını sağlar
    /// </summary>
    [HttpPost("customers/{customerId:guid}/features/set")]
    public async Task<ActionResult<ApiResponse<CustomerFeatureDto>>> SetCustomerFeature(Guid customerId, [FromBody] SetCustomerFeatureDto dto)
    {
        var adminUsername = User.Identity?.Name ?? "admin";

        try
        {
            var updated = await _featureService.SetCustomerFeatureAsync(customerId, dto, adminUsername);
            return Success(updated, "Müşteri özelliği başarıyla güncellendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CustomerFeatureDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CustomerFeatureDto>(ex.Message);
        }
    }

    /// <summary>
    /// Admin'in müşteriye birden çok özelliği tek seferde toplu olarak güncellemesini sağlar
    /// </summary>
    [HttpPost("customers/{customerId:guid}/features/bulk")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerFeatureDto>>>> BulkUpdateCustomerFeatures(Guid customerId, [FromBody] List<SetCustomerFeatureDto> features)
    {
        var adminUsername = User.Identity?.Name ?? "admin";

        try
        {
            var updated = await _featureService.BulkUpdateCustomerFeaturesAsync(customerId, features, adminUsername);
            return Success(updated, "Müşteri özellikleri toplu olarak güncellendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<IEnumerable<CustomerFeatureDto>>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<IEnumerable<CustomerFeatureDto>>(ex.Message);
        }
    }
}

