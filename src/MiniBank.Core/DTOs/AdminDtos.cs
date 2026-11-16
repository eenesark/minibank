namespace MiniBank.DTOs;

public record AdminResponseDto(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt
);

public record UpdateAdminDto(
    string Username,
    string FullName,
    string Email,
    string PhoneNumber,
    string CurrentPassword,
    string? NewPassword = null
);

public record CreateAdminDto(
    string Username,
    string FullName,
    string Email,
    string PhoneNumber,
    string Password
);

public record AdminDashboardDto(
    int TotalCustomers,
    int TotalAccounts,
    decimal TotalBankBalance,
    int TotalTransactions,
    int ActiveAccountsCount
);

