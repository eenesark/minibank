using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;

namespace MiniBank.Services;

public interface IAccountService
{
    Task<IEnumerable<AccountResponseDto>> GetAllAsync();
    Task<AccountResponseDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AccountResponseDto>> GetByCustomerIdAsync(Guid customerId);
    Task<AccountResponseDto> CreateAsync(CreateAccountDto dto);
    Task<AccountResponseDto> UpdateAsync(Guid id, UpdateAccountDto dto);
}

public class AccountService : IAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IUnitOfWork unitOfWork, ILogger<AccountService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<AccountResponseDto>> GetAllAsync()
    {
        var accounts = await _unitOfWork.Accounts.GetAccountsWithCustomerAsync();

        return accounts.Select(a => new AccountResponseDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive,
            a.CustomerId,
            a.Customer != null ? $"{a.Customer.FirstName} {a.Customer.LastName}" : string.Empty,
            a.CreatedAt,
            a.AccountType,
            a.MaturityDays,
            a.InterestRate,
            a.MaturityDate,
            a.ExpectedInterestAmount
        ));
    }

    public async Task<AccountResponseDto?> GetByIdAsync(Guid id)
    {
        var account = await _unitOfWork.Accounts.GetAccountWithCustomerByIdAsync(id);
        if (account is null) return null;

        return new AccountResponseDto(
            account.Id,
            account.AccountNumber,
            account.Iban,
            account.Balance,
            account.Currency,
            account.IsActive,
            account.CustomerId,
            $"{account.Customer.FirstName} {account.Customer.LastName}",
            account.CreatedAt,
            account.AccountType,
            account.MaturityDays,
            account.InterestRate,
            account.MaturityDate,
            account.ExpectedInterestAmount
        );
    }

    public async Task<IEnumerable<AccountResponseDto>> GetByCustomerIdAsync(Guid customerId)
    {
        var accounts = await _unitOfWork.Accounts.GetAccountsByCustomerIdAsync(customerId);

        return accounts.Select(a => new AccountResponseDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive,
            a.CustomerId,
            a.Customer != null ? $"{a.Customer.FirstName} {a.Customer.LastName}" : string.Empty,
            a.CreatedAt,
            a.AccountType,
            a.MaturityDays,
            a.InterestRate,
            a.MaturityDate,
            a.ExpectedInterestAmount
        ));
    }

    public async Task<AccountResponseDto> CreateAsync(CreateAccountDto dto)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Hesap açılacak müşteri bulunamadı.");
        }

        var random = new Random();
        var accountNumber = !string.IsNullOrWhiteSpace(dto.CustomAccountNumber)
            ? dto.CustomAccountNumber.Trim()
            : random.NextInt64(1000000000, 9999999999).ToString();

        var iban = !string.IsNullOrWhiteSpace(dto.CustomIban)
            ? dto.CustomIban.Trim()
            : $"TR{random.Next(10, 99)}000620{accountNumber}";

        var account = new Account
        {
            CustomerId = customer.Id,
            Customer = customer,
            AccountNumber = accountNumber,
            Iban = iban,
            Currency = dto.Currency.ToUpper(),
            Balance = dto.InitialDeposit,
            IsActive = true
        };

        if (dto.InitialDeposit > 0)
        {
            account.Transactions.Add(new AccountTransaction
            {
                Amount = dto.InitialDeposit,
                Type = TransactionType.Deposit,
                Description = "Açılış İlk Bakiye Yüklemesi"
            });
        }

        await _unitOfWork.Accounts.AddAsync(account);

        // Yeni açılan her hesaba otomatik olarak bir banka kartı (Debit Card) tanımla
        var cardHolder = $"{customer.FirstName} {customer.LastName}".ToUpperInvariant();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            AccountId = account.Id,
            CardNumber = $"454362{random.Next(10, 99)}{random.Next(1000, 9999)}{random.Next(1000, 9999)}",
            CardHolderName = cardHolder,
            ExpiryMonth = DateTime.UtcNow.Month,
            ExpiryYear = (DateTime.UtcNow.Year % 100) + 4,
            Cvv = random.Next(100, 999).ToString(),
            CardType = "Debit",
            Status = CardStatus.Active,
            IsInternationalAllowed = true,
            IsOnlineShoppingAllowed = true,
            IsContactlessAllowed = true,
            DailyLimit = 15000.00m,
            DailyAtmWithdrawLimit = 5000.00m,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Cards.AddAsync(card);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Yeni hesap ve banka kartı açıldı: {Iban} - Kart: {CardNumber} - Müşteri: {CustomerId}", 
            account.Iban, card.CardNumber, customer.Id);

        return new AccountResponseDto(
            account.Id,
            account.AccountNumber,
            account.Iban,
            account.Balance,
            account.Currency,
            account.IsActive,
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            account.CreatedAt,
            account.AccountType,
            account.MaturityDays,
            account.InterestRate,
            account.MaturityDate,
            account.ExpectedInterestAmount
        );
    }

    public async Task<AccountResponseDto> UpdateAsync(Guid id, UpdateAccountDto dto)
    {
        var account = await _unitOfWork.Accounts.GetAccountWithCustomerByIdAsync(id);
        if (account is null)
        {
            throw new KeyNotFoundException("Güncellenecek hesap bulunamadı.");
        }

        // Manuel Hesap No değişikliği
        if (!string.IsNullOrWhiteSpace(dto.AccountNumber))
        {
            account.AccountNumber = dto.AccountNumber.Trim();
        }

        // Manuel IBAN değişikliği
        if (!string.IsNullOrWhiteSpace(dto.Iban))
        {
            var newIban = dto.Iban.Trim();
            var exists = await _unitOfWork.Accounts.AnyAsync(a => a.Iban == newIban && a.Id != id);
            if (exists)
            {
                throw new InvalidOperationException("Bu IBAN başka bir hesaba kayıtlı.");
            }
            account.Iban = newIban;
        }

        // Para birimi değişikliği
        if (!string.IsNullOrWhiteSpace(dto.Currency))
        {
            account.Currency = dto.Currency.ToUpper().Trim();
        }

        // Aktiflik/Pasiflik durumu
        if (dto.IsActive.HasValue)
        {
            account.IsActive = dto.IsActive.Value;
        }

        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Hesap bilgileri güncellendi: {AccountId} - Yeni IBAN: {Iban}",
            account.Id, account.Iban);

        return new AccountResponseDto(
            account.Id,
            account.AccountNumber,
            account.Iban,
            account.Balance,
            account.Currency,
            account.IsActive,
            account.CustomerId,
            $"{account.Customer.FirstName} {account.Customer.LastName}",
            account.CreatedAt,
            account.AccountType,
            account.MaturityDays,
            account.InterestRate,
            account.MaturityDate,
            account.ExpectedInterestAmount
        );
    }
}

