using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using MiniBank.Services.Generators;
using MiniBank.Services.Security;

namespace MiniBank.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerResponseDto>> GetAllAsync();
    Task<CustomerResponseDto?> GetByIdAsync(Guid id);
    Task<CustomerResponseDto> CreateAsync(CreateCustomerDto dto);
    Task<CustomerResponseDto> UpdateAsync(Guid id, UpdateCustomerDto dto);
}

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBankingCodeGenerator _codeGenerator;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IBankingCodeGenerator codeGenerator,
        ILogger<CustomerService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerResponseDto>> GetAllAsync()
    {
        var customers = await _unitOfWork.Customers.GetCustomersWithAccountsAsync();

        return customers.Select(c => new CustomerResponseDto(
            c.Id,
            c.CustomerNumber,
            c.FirstName,
            c.LastName,
            c.IdentityNumber,
            c.Email,
            c.PhoneNumber,
            c.CreatedAt,
            c.Accounts.Select(a => new CustomerAccountSummaryDto(
                a.Id,
                a.AccountNumber,
                a.Iban,
                a.Balance,
                a.Currency,
                a.IsActive
            )).FirstOrDefault()
        ));
    }

    public async Task<CustomerResponseDto?> GetByIdAsync(Guid id)
    {
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsByIdAsync(id);
        if (customer is null) return null;

        var defaultAccount = customer.Accounts.Select(a => new CustomerAccountSummaryDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive
        )).FirstOrDefault();

        return new CustomerResponseDto(
            customer.Id,
            customer.CustomerNumber,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            defaultAccount
        );
    }

    public async Task<CustomerResponseDto> CreateAsync(CreateCustomerDto dto)
    {
        var exists = await _unitOfWork.Customers.AnyAsync(c => c.IdentityNumber == dto.IdentityNumber);
        if (exists)
        {
            _logger.LogWarning("Müşteri kaydı başarısız: TC No zaten mevcut. {IdentityNumber}", dto.IdentityNumber);
            throw new InvalidOperationException("Bu T.C. Kimlik Numarasına sahip bir müşteri zaten kayıtlı.");
        }

        // 1. Müşteri Numarasını Üret (IBankingCodeGenerator - SRP)
        var customerNumber = _codeGenerator.GenerateCustomerNumber();

        // 2. Şifreyi Hashle (IPasswordHasher - SRP & DIP)
        var initialPassword = string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password;
        _passwordHasher.CreatePasswordHash(initialPassword, out var hash, out var salt);

        var customer = new Customer
        {
            CustomerNumber = customerNumber,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            IdentityNumber = dto.IdentityNumber.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "Customer"
        };

        // 3. Müşteriye Otomatik Banka Hesabı ve IBAN Aç (IBankingCodeGenerator - SRP)
        var accountNumber = _codeGenerator.GenerateAccountNumber();
        var iban = _codeGenerator.GenerateIban(accountNumber);

        var account = new Account
        {
            CustomerId = customer.Id,
            Customer = customer,
            AccountNumber = accountNumber,
            Iban = iban,
            Currency = "TRY",
            Balance = 0,
            IsActive = true
        };

        account.Customer = customer;
        customer.Accounts.Add(account);

        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.Accounts.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Yeni müşteri ve otomatik hesabı oluşturuldu: {CustomerId} ({CustomerNumber}) - IBAN: {Iban}",
            customer.Id, customer.CustomerNumber, account.Iban);

        var accountSummary = new CustomerAccountSummaryDto(
            account.Id,
            account.AccountNumber,
            account.Iban,
            account.Balance,
            account.Currency,
            account.IsActive
        );

        return new CustomerResponseDto(
            customer.Id,
            customer.CustomerNumber,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            accountSummary
        );
    }

    public async Task<CustomerResponseDto> UpdateAsync(Guid id, UpdateCustomerDto dto)
    {
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsTrackingByIdAsync(id);
        if (customer is null)
        {
            throw new KeyNotFoundException("Güncellenecek müşteri bulunamadı.");
        }

        if (!string.IsNullOrWhiteSpace(dto.CustomerNumber))
        {
            customer.CustomerNumber = dto.CustomerNumber.Trim();
        }

        customer.FirstName = dto.FirstName.Trim();
        customer.LastName = dto.LastName.Trim();
        customer.Email = dto.Email.Trim().ToLowerInvariant();
        customer.PhoneNumber = dto.PhoneNumber.Trim();

        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Müşteri bilgileri güncellendi: {CustomerId} ({CustomerNumber})",
            customer.Id, customer.CustomerNumber);

        var defaultAccount = customer.Accounts.Select(a => new CustomerAccountSummaryDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive
        )).FirstOrDefault();

        return new CustomerResponseDto(
            customer.Id,
            customer.CustomerNumber,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            defaultAccount
        );
    }
}

