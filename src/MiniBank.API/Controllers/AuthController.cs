using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class AuthController : BaseApiController
{
    private readonly IAuthenticationService _authService;
    private readonly IUserProfileService _userProfileService;

    public AuthController(
        IAuthenticationService authService,
        IUserProfileService userProfileService)
    {
        _authService = authService;
        _userProfileService = userProfileService;
    }

    /// <summary>   
    /// Sisteme giriş yapar (Hem Admin hem de Müşteri için ortaktır)
    /// </summary>
    /// <param name="dto">T.C. Kimlik No (veya admin kullanıcı adı) ve Şifre</param>
    /// <returns>JWT Bearer Token ve Kullanıcı Rolü</returns>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Success(result, "Giriş işlemi başarılı.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorUnauthorized<AuthResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Yeni müşteri kayıt ekranı (Self-Registration)
    /// Bilgileri alır, hesabı ve IBAN'ı otomatik açar, giriş token'ı döner
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);
            return CreatedSuccess(result, "Müşteri kaydı ve hesap açılışı başarıyla tamamlandı.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<AuthResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Şifremi Unuttum / İlk Şifremi Belirle
    /// T.C. Kimlik No ve sistemde kayıtlı E-Posta veya Telefon ile doğrulama yaparak yeni şifreyi tanımlar
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
            return Success("Şifreniz başarıyla belirlendi/güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan kullanıcının (müşteri veya admin) kendi profilini ve hesaplarını getirir
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<MeResponseDto>>> GetMe()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return ErrorUnauthorized<MeResponseDto>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var me = await _userProfileService.GetMeAsync(userId);
            return Success(me, "Kullanıcı profil ve hesap bilgileri başarıyla getirildi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<MeResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan kullanıcının kendi profil bilgilerini güncellemesini sağlar.
    /// Güvenlik amacıyla mevcut şifrenin doğrulanması zorunludur.
    /// </summary>
    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<MeResponseDto>>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return ErrorUnauthorized<MeResponseDto>("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            var updated = await _userProfileService.UpdateProfileAsync(userId, dto);
            return Success(updated, "Profil bilgileri başarıyla güncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<MeResponseDto>(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<MeResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan kullanıcının kendi şifresini değiştirmesini sağlar
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return ErrorUnauthorized("Oturum bilgisi doğrulanamadı.");
        }

        try
        {
            await _userProfileService.ChangePasswordAsync(userId, dto);
            return Success("Şifreniz başarıyla güncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound(ex.Message);
        }
    }
}

