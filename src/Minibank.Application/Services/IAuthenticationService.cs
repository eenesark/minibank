using MiniBank.DTOs;

namespace MiniBank.Services;

public interface IAuthenticationService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task ResetPasswordAsync(ForgotPasswordDto dto);
}

