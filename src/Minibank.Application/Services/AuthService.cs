using MiniBank.DTOs;
using MiniBank.Services.Security;

namespace MiniBank.Services;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task ResetPasswordAsync(ForgotPasswordDto dto);
    Task<MeResponseDto> GetMeAsync(Guid currentUserId);
    Task<MeResponseDto> UpdateProfileAsync(Guid currentUserId, UpdateProfileDto dto);
    Task ChangePasswordAsync(Guid currentUserId, ChangePasswordDto dto);
}

/// <summary>
/// AuthService Facade: Geriye dönük uyumluluk sağlamak amacıyla IAuthenticationService ve IUserProfileService
/// soyutlamalarını birleştiren bir Facade görevi görür.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserProfileService _userProfileService;
    private static readonly IPasswordHasher _staticPasswordHasher = new PasswordHasher();

    public AuthService(
        IAuthenticationService authenticationService,
        IUserProfileService userProfileService)
    {
        _authenticationService = authenticationService;
        _userProfileService = userProfileService;
    }

    public Task<AuthResponseDto> LoginAsync(LoginDto dto) =>
        _authenticationService.LoginAsync(dto);

    public Task<AuthResponseDto> RegisterAsync(RegisterDto dto) =>
        _authenticationService.RegisterAsync(dto);

    public Task ResetPasswordAsync(ForgotPasswordDto dto) =>
        _authenticationService.ResetPasswordAsync(dto);

    public Task<MeResponseDto> GetMeAsync(Guid currentUserId) =>
        _userProfileService.GetMeAsync(currentUserId);

    public Task<MeResponseDto> UpdateProfileAsync(Guid currentUserId, UpdateProfileDto dto) =>
        _userProfileService.UpdateProfileAsync(currentUserId, dto);

    public Task ChangePasswordAsync(Guid currentUserId, ChangePasswordDto dto) =>
        _userProfileService.ChangePasswordAsync(currentUserId, dto);

    // Geriye dönük uyumluluk için statik köprü metotları
    public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) =>
        _staticPasswordHasher.CreatePasswordHash(password, out passwordHash, out passwordSalt);

    public static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt) =>
        _staticPasswordHasher.VerifyPasswordHash(password, storedHash, storedSalt);
}

