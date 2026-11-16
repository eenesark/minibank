using System.Text.Json.Serialization;

namespace MiniBank.Common;

/// <summary>
/// Tüm API istekleri için standart, kurumsal ve tip-güvenli yanıt şablonu
/// </summary>
/// <typeparam name="T">Dönen verinin türü</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // JSON Serileştirme için parametresiz constructor
    public ApiResponse()
    {
    }

    private ApiResponse(bool success, int statusCode, string? message, T? data, List<string>? errors)
    {
        Success = success;
        StatusCode = statusCode;
        Message = message;
        Data = data;
        Errors = errors;
    }

    #region Başarılı Yanıt Fabrika Metotları (Factory Methods)

    public static ApiResponse<T> Ok(T data, string? message = "İşlem başarılı.")
    {
        return new ApiResponse<T>(true, 200, message, data, null);
    }

    public static ApiResponse<T> Created(T data, string? message = "Kayıt başarıyla oluşturuldu.")
    {
        return new ApiResponse<T>(true, 201, message, data, null);
    }

    #endregion

    #region Hatalı Yanıt Fabrika Metotları

    public static ApiResponse<T> Fail(string error, int statusCode = 400)
    {
        return new ApiResponse<T>(false, statusCode, error, default, new List<string> { error });
    }

    public static ApiResponse<T> Fail(List<string> errors, string message = "İşlem sırasında bir hata oluştu.", int statusCode = 400)
    {
        return new ApiResponse<T>(false, statusCode, message, default, errors);
    }

    #endregion
}

/// <summary>
/// Geriye veri (Data) dönmeyen, sadece başarı/başarısızlık bildiren yanıtlar için genel sınıf
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse SuccessResult(string message = "İşlem başarılı.", int statusCode = 200)
    {
        return new ApiResponse
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = null,
            Errors = null
        };
    }

    public static new ApiResponse Fail(string error, int statusCode = 400)
    {
        return new ApiResponse
        {
            Success = false,
            StatusCode = statusCode,
            Message = error,
            Data = null,
            Errors = new List<string> { error }
        };
    }

    public static new ApiResponse Fail(List<string> errors, string message = "İşlem başarısız.", int statusCode = 400)
    {
        return new ApiResponse
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Data = null,
            Errors = errors
        };
    }
}

