using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Services.Security;

namespace MiniBank.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<UserProfileService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<MeResponseDto> GetMeAsync(Guid currentUserId)
    {
        // 1. Önce bağımsız Admin tablosunda ara
        var admin = await _unitOfWork.Admins.GetByIdAsync(currentUserId);
        if (admin is not null)
        {
            return new MeResponseDto(
                admin.Id,
                "ADM000001",
                admin.FullName,
                "",
                admin.Username,
                admin.Email,
                admin.PhoneNumber,
                "Admin",
                Enumerable.Empty<AccountResponseDto>()
            );
        }

        // 2. Müşteri tablosunda ara
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsByIdAsync(currentUserId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Giriş yapan kullanıcı profili bulunamadı.");
        }

        var accountsDto = customer.Accounts.Select(a => new AccountResponseDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive,
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            a.CreatedAt,
            a.AccountType,
            a.MaturityDays,
            a.InterestRate,
            a.MaturityDate,
            a.ExpectedInterestAmount
        ));

        return new MeResponseDto(
            customer.Id,
            customer.CustomerNumber,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.Role,
            accountsDto
        );
    }

    public async Task<MeResponseDto> UpdateProfileAsync(Guid currentUserId, UpdateProfileDto dto)
    {
        // 1. Önce bağımsız Admin tablosunda ara
        var admin = await _unitOfWork.Admins.GetByIdAsync(currentUserId);
        if (admin is not null)
        {
            if (!_passwordHasher.VerifyPasswordHash(dto.CurrentPassword, admin.PasswordHash, admin.PasswordSalt))
            {
                _logger.LogWarning("Admin profil güncelleme başarısız: Şifre uyuşmuyor ({AdminId})", currentUserId);
                throw new InvalidOperationException("Güvenlik doğrulaması başarısız: Mevcut şifreniz hatalı.");
            }

            var newAdminEmail = dto.Email.Trim().ToLowerInvariant();
            admin.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
            admin.Email = newAdminEmail;
            admin.PhoneNumber = dto.PhoneNumber.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                _passwordHasher.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
                admin.PasswordHash = hash;
                admin.PasswordSalt = salt;
                _logger.LogInformation("Admin şifresini profil üzerinden güncelledi: {AdminId}", currentUserId);
            }

            _unitOfWork.Admins.Update(admin);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Admin profil bilgileri güncellendi: {AdminId}", currentUserId);

            return new MeResponseDto(
                admin.Id,
                "ADM000001",
                admin.FullName,
                "",
                admin.Username,
                admin.Email,
                admin.PhoneNumber,
                "Admin",
                Enumerable.Empty<AccountResponseDto>()
            );
        }

        // 2. Müşteri profil güncelleme
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsTrackingByIdAsync(currentUserId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Kullanıcı profili bulunamadı.");
        }

        if (customer.PasswordHash is not null && customer.PasswordSalt is not null)
        {
            if (!_passwordHasher.VerifyPasswordHash(dto.CurrentPassword, customer.PasswordHash, customer.PasswordSalt))
            {
                _logger.LogWarning("Profil güncelleme başarısız: Şifre uyuşmuyor ({CustomerId})", currentUserId);
                throw new InvalidOperationException("Güvenlik doğrulaması başarısız: Mevcut şifreniz hatalı.");
            }
        }
        else
        {
            throw new InvalidOperationException("Hesap şifresi tanımlanmamış. Lütfen önce şifrenizi belirleyin.");
        }

        var newEmail = dto.Email.Trim().ToLowerInvariant();
        if (!string.Equals(customer.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _unitOfWork.Customers.AnyAsync(c => c.Id != customer.Id && c.Email.ToLower() == newEmail);
            if (emailExists)
            {
                throw new InvalidOperationException("Bu e-posta adresi başka bir müşteri tarafından kullanılmaktadır.");
            }
            customer.Email = newEmail;
        }

        customer.FirstName = dto.FirstName.Trim();
        customer.LastName = dto.LastName.Trim();
        customer.PhoneNumber = dto.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            _passwordHasher.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
            customer.PasswordHash = hash;
            customer.PasswordSalt = salt;
            _logger.LogInformation("Kullanıcı profil güncelleme sırasında şifresini de değiştirdi: {CustomerId}", currentUserId);
        }

        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı profil bilgileri başarıyla güncellendi: {CustomerId}", currentUserId);

        var accountsDto = customer.Accounts.Select(a => new AccountResponseDto(
            a.Id,
            a.AccountNumber,
            a.Iban,
            a.Balance,
            a.Currency,
            a.IsActive,
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            a.CreatedAt,
            a.AccountType,
            a.MaturityDays,
            a.InterestRate,
            a.MaturityDate,
            a.ExpectedInterestAmount
        ));

        return new MeResponseDto(
            customer.Id,
            customer.CustomerNumber,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.Role,
            accountsDto
        );
    }

    public async Task ChangePasswordAsync(Guid currentUserId, ChangePasswordDto dto)
    {
        // 1. Admin kontrolü
        var admin = await _unitOfWork.Admins.GetByIdAsync(currentUserId);
        if (admin is not null)
        {
            if (!_passwordHasher.VerifyPasswordHash(dto.OldPassword, admin.PasswordHash, admin.PasswordSalt))
            {
                throw new InvalidOperationException("Mevcut şifreniz hatalı.");
            }

            _passwordHasher.CreatePasswordHash(dto.NewPassword, out var aHash, out var aSalt);
            admin.PasswordHash = aHash;
            admin.PasswordSalt = aSalt;
            _unitOfWork.Admins.Update(admin);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Admin şifresini güncelledi: {AdminId}", currentUserId);
            return;
        }

        // 2. Müşteri kontrolü
        var customer = await _unitOfWork.Customers.GetByIdAsync(currentUserId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        }

        if (customer.PasswordHash is not null && customer.PasswordSalt is not null)
        {
            if (!_passwordHasher.VerifyPasswordHash(dto.OldPassword, customer.PasswordHash, customer.PasswordSalt))
            {
                throw new InvalidOperationException("Mevcut şifreniz hatalı.");
            }
        }

        _passwordHasher.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
        customer.PasswordHash = hash;
        customer.PasswordSalt = salt;

        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı şifresini güncelledi: {CustomerId}", currentUserId);
    }
}

