using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;

namespace MiniBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// JWT Token'dan giriş yapan kullanıcının Guid Id'sini döner.
    /// Token yoksa veya parse edilemezse null döner.
    /// </summary>
    protected Guid? CurrentUserId
    {
        get
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var id))
            {
                return id;
            }
            return null;
        }
    }

    /// <summary>
    /// Kullanıcının oturum açıp açmadığını ve geçerli bir Id'ye sahip olup olmadığını doğrular
    /// </summary>
    protected bool TryGetCurrentUserId(out Guid userId)
    {
        userId = CurrentUserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    #region Ortak HTTP Yanıt Yardımcı Metotları (Helpers)

    /// <summary>
    /// 200 OK ile standart başarılı yanıt döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = "İşlem başarılı.")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    /// <summary>
    /// 200 OK ile veri içermeyen başarılı işlem mesajı döner
    /// </summary>
    protected ActionResult<ApiResponse> Success(string message = "İşlem başarılı.")
    {
        return Ok(ApiResponse.SuccessResult(message));
    }

    /// <summary>
    /// 201 Created ile yeni oluşturulan kaydı döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> CreatedSuccess<T>(T data, string? message = "Kayıt başarıyla oluşturuldu.")
    {
        return StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Created(data, message));
    }

    /// <summary>
    /// 400 Bad Request ile hata mesajı döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> ErrorBadRequest<T>(string errorMessage)
    {
        return BadRequest(ApiResponse<T>.Fail(errorMessage, 400));
    }

    /// <summary>
    /// 400 Bad Request ile veri içermeyen hata mesajı döner
    /// </summary>
    protected ActionResult<ApiResponse> ErrorBadRequest(string errorMessage)
    {
        return BadRequest(ApiResponse.Fail(errorMessage, 400));
    }

    /// <summary>
    /// 404 Not Found ile bulunamadı hatası döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> ErrorNotFound<T>(string errorMessage = "Aradığınız kayıt bulunamadı.")
    {
        return NotFound(ApiResponse<T>.Fail(errorMessage, 404));
    }

    /// <summary>
    /// 404 Not Found ile veri içermeyen hata döner
    /// </summary>
    protected ActionResult<ApiResponse> ErrorNotFound(string errorMessage = "Aradığınız kayıt bulunamadı.")
    {
        return NotFound(ApiResponse.Fail(errorMessage, 404));
    }

    /// <summary>
    /// 401 Unauthorized ile yetkisiz işlem hatası döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> ErrorUnauthorized<T>(string errorMessage = "Oturum bilgisi doğrulanamadı veya yetkisiz erişim.")
    {
        return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<T>.Fail(errorMessage, 401));
    }

    /// <summary>
    /// 401 Unauthorized ile yetkisiz işlem hatası döner
    /// </summary>
    protected ActionResult<ApiResponse> ErrorUnauthorized(string errorMessage = "Oturum bilgisi doğrulanamadı veya yetkisiz erişim.")
    {
        return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail(errorMessage, 401));
    }

    /// <summary>
    /// 403 Forbidden ile yasaklı işlem hatası döner
    /// </summary>
    protected ActionResult<ApiResponse<T>> ErrorForbidden<T>(string errorMessage = "Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır.")
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail(errorMessage, 403));
    }

    /// <summary>
    /// 403 Forbidden ile yasaklı işlem hatası döner
    /// </summary>
    protected ActionResult<ApiResponse> ErrorForbidden(string errorMessage = "Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır.")
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(errorMessage, 403));
    }

    #endregion
}
