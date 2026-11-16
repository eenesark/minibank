using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using MiniBank.Services.Generators;
using MiniBank.Services.Security;

namespace MiniBank.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IBankingCodeGenerator _codeGenerator;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IBankingCodeGenerator codeGenerator,
        ILogger<AuthenticationService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var input = dto.IdentityNumberOrUsername.Trim().ToLowerInvariant();

        // 1. Önce bağımsız Admins tablosunda ara
        var admin = await _unitOfWork.Admins.GetByUsernameOrEmailAsync(input);

        if (admin is not null)
        {
            if (!_passwordHasher.VerifyPasswordHash(dto.Password, admin.PasswordHash, admin.PasswordSalt))
            {
                _logger.LogWarning("Admin girişi başarısız: Şifre uyuşmuyor ({Input})", input);
                throw new UnauthorizedAccessException("T.C. Kimlik No / Kullanıcı Adı veya Şifre hatalı.");
            }

            var adminToken = _tokenService.GenerateToken(
                admin.Id,
                admin.FullName,
                "Admin",
                "ADM000001",
                admin.Username,
                admin.Email
            );

            _logger.LogInformation("Admin başarıyla giriş yaptı: {AdminId} ({Username})", admin.Id, admin.Username);

            return new AuthResponseDto(
                adminToken,
                "Admin",
                admin.Id,
                admin.FullName,
                "ADM000001",
                admin.Username
            );
        }

        // 2. Admin değilse Customers tablosunda ara
        var customer = await _unitOfWork.Customers.GetByIdentityNumberOrEmailAsync(input);

        if (customer is null)
        {
            _logger.LogWarning("Giriş başarısız: Kullanıcı bulunamadı ({Input})", input);
            throw new UnauthorizedAccessException("T.C. Kimlik No / Kullanıcı Adı veya Şifre hatalı.");
        }

        if (customer.PasswordHash is null || customer.PasswordSalt is null)
        {
            _logger.LogWarning("Giriş başarısız: Kullanıcı şifresi henüz tanımlanmamış ({Input})", input);
            throw new UnauthorizedAccessException("Bu kullanıcının şifresi henüz tanımlanmamış. Lütfen yönetici ile iletişime geçin.");
        }

        if (!_passwordHasher.VerifyPasswordHash(dto.Password, customer.PasswordHash, customer.PasswordSalt))
        {
            _logger.LogWarning("Giriş başarısız: Şifre uyuşmuyor ({Input})", input);
            throw new UnauthorizedAccessException("T.C. Kimlik No / Kullanıcı Adı veya Şifre hatalı.");
        }

        var token = _tokenService.GenerateToken(
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            customer.Role,
            customer.CustomerNumber,
            customer.IdentityNumber,
            customer.Email
        );

        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı: {CustomerId} ({Role})", customer.Id, customer.Role);

        return new AuthResponseDto(
            token,
            customer.Role,
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            customer.CustomerNumber,
            customer.IdentityNumber
        );
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var identity = dto.IdentityNumber.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();

        var exists = await _unitOfWork.Customers.AnyAsync(c => c.IdentityNumber == identity);
        if (exists)
        {
            throw new InvalidOperationException("Bu T.C. Kimlik Numarası ile zaten kayıtlı bir müşteri bulunmaktadır.");
        }

        var emailExists = await _unitOfWork.Customers.AnyAsync(c => c.Email.ToLower() == email);
        if (emailExists)
        {
            throw new InvalidOperationException("Bu e-posta adresi ile zaten kayıtlı bir müşteri bulunmaktadır.");
        }

        // Şifre Hashleme
        _passwordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

        // Müşteri No ve Hesap Bilgileri Üretme
        var customerNumber = _codeGenerator.GenerateCustomerNumber();
        var accountNumber = _codeGenerator.GenerateAccountNumber();
        var iban = _codeGenerator.GenerateIban(accountNumber);

        var customer = new Customer
        {
            CustomerNumber = customerNumber,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            IdentityNumber = identity,
            Email = email,
            PhoneNumber = dto.PhoneNumber.Trim(),
            Role = "Customer",
            PasswordHash = hash,
            PasswordSalt = salt
        };

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

        customer.Accounts.Add(account);

        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.Accounts.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Yeni müşteri kayıt oldu: {CustomerId} ({CustomerNumber}) - IBAN: {Iban}",
            customer.Id, customer.CustomerNumber, account.Iban);

        var token = _tokenService.GenerateToken(
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            customer.Role,
            customer.CustomerNumber,
            customer.IdentityNumber,
            customer.Email
        );

        return new AuthResponseDto(
            token,
            customer.Role,
            customer.Id,
            $"{customer.FirstName} {customer.LastName}",
            customer.CustomerNumber,
            customer.IdentityNumber
        );
    }

    public async Task ResetPasswordAsync(ForgotPasswordDto dto)
    {
        var identity = dto.IdentityNumber.Trim().ToLowerInvariant();
        var inputCheck = dto.EmailOrPhone.Trim().ToLowerInvariant().Replace(" ", "");

        // 1. Güvenlik Kuralı: Admin şifresi 'Şifremi Unuttum' ekranından değiştirilemez!
        var isAdmin = await _unitOfWork.Admins.AnyAsync(a => a.Username.ToLower() == identity || a.Email.ToLower() == identity);
        if (isAdmin)
        {
            _logger.LogWarning("Şifre sıfırlama reddedildi: Yönetici şifresi bu ekrandan değiştirilemez ({Identity})", identity);
            throw new InvalidOperationException("Yönetici şifresi bu ekrandan sıfırlanamaz. Yöneticiler şifrelerini sisteme giriş yaptıktan sonra 'Profilim' ekranından değiştirebilir.");
        }

        // 2. Yalnızca Müşteriler tablosunda ara
        var customer = await _unitOfWork.Customers.GetByIdentityNumberAsync(identity);
        if (customer is null)
        {
            _logger.LogWarning("Şifre sıfırlama başarısız: TC/Kullanıcı bulunamadı ({Identity})", identity);
            throw new KeyNotFoundException("Belirtilen T.C. Kimlik Numarası veya Kullanıcı Adı ile eşleşen bir kayıt bulunamadı.");
        }

        // Güvenlik doğrulaması: E-Posta veya Telefon uyuşuyor mu?
        var emailClean = customer.Email.Trim().ToLowerInvariant();
        var phoneClean = customer.PhoneNumber.Trim().Replace(" ", "").Replace("+", "");
        var inputClean = inputCheck.Replace("+", "");

        var isEmailMatch = emailClean == inputClean;
        var isPhoneMatch = phoneClean.Contains(inputClean) || inputClean.Contains(phoneClean);

        if (!isEmailMatch && !isPhoneMatch)
        {
            _logger.LogWarning("Şifre sıfırlama başarısız: Güvenlik bilgisi uyuşmadı ({Identity})", identity);
            throw new InvalidOperationException("Güvenlik doğrulaması başarısız: T.C. Kimlik No ile kayıtlı E-Posta veya Telefon bilgisi uyuşmuyor.");
        }

        // Yeni şifreyi kaydet
        _passwordHasher.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
        customer.PasswordHash = hash;
        customer.PasswordSalt = salt;

        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Müşteri şifresini sıfırladı/yeniledi: {CustomerId} ({CustomerNumber})", customer.Id, customer.CustomerNumber);
    }
}

