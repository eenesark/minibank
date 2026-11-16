namespace MiniBank.DTOs;

public record LoginDto(
    string IdentityNumberOrUsername,
    string Password
);

public record AuthResponseDto(
    string Token,
    string Role,
    Guid UserId,
    string FullName,
    string CustomerNumber,
    string IdentityNumber
);

public record MeResponseDto(
    Guid Id,
    string CustomerNumber,
    string FirstName,
    string LastName,
    string IdentityNumber,
    string Email,
    string PhoneNumber,
    string Role,
    IEnumerable<AccountResponseDto> Accounts
);

public record ChangePasswordDto(
    string OldPassword,
    string NewPassword
);

public record RegisterDto(
    string FirstName,
    string LastName,
    string IdentityNumber,
    string Email,
    string PhoneNumber,
    string Password
);

public record ForgotPasswordDto(
    string IdentityNumber,
    string EmailOrPhone,
    string NewPassword
);

public record UpdateProfileDto(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string CurrentPassword,
    string? NewPassword = null
);

