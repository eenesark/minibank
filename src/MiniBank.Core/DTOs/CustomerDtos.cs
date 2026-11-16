namespace MiniBank.DTOs;

public record CreateCustomerDto(
    string FirstName,
    string LastName,
    string IdentityNumber,
    string Email,
    string PhoneNumber,
    string? Password = null
);

public record UpdateCustomerDto(
    string? CustomerNumber,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber
);

public record CustomerAccountSummaryDto(
    Guid Id,
    string AccountNumber,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsActive
);

public record CustomerResponseDto(
    Guid Id,
    string CustomerNumber,
    string FirstName,
    string LastName,
    string IdentityNumber,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    CustomerAccountSummaryDto? Account = null
);

