using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using MiniBank.Services.Security;

namespace MiniBank.Services;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<AdminService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var totalCustomers = await _unitOfWork.Customers.CountAsync(c => c.Role == "Customer");
        var totalAccounts = await _unitOfWork.Accounts.CountAsync();
        var totalBankBalance = await _unitOfWork.Accounts.GetTotalBalanceAsync();
        var totalTransactions = await _unitOfWork.Transactions.CountAsync();
        var activeAccountsCount = await _unitOfWork.Accounts.GetActiveAccountsCountAsync();

        return new AdminDashboardDto(
            totalCustomers,
            totalAccounts,
            totalBankBalance,
            totalTransactions,
            activeAccountsCount
        );
    }

    public async Task<IEnumerable<AdminResponseDto>> GetAllAdminsAsync()
    {
        var admins = await _unitOfWork.Admins.GetAllAdminsAsync();

        return admins.Select(a => new AdminResponseDto(
            a.Id,
            a.Username,
            a.FullName,
            a.Email,
            a.PhoneNumber,
            a.CreatedAt
        ));
    }

    public async Task<AdminResponseDto?> GetAdminByIdAsync(Guid id)
    {
        var admin = await _unitOfWork.Admins.GetByIdAsync(id);
        if (admin is null) return null;

        return new AdminResponseDto(
            admin.Id,
            admin.Username,
            admin.FullName,
            admin.Email,
            admin.PhoneNumber,
            admin.CreatedAt
        );
    }

    public async Task<AdminResponseDto?> GetMeAsync(Guid? currentUserId)
    {
        AdminUser? admin = null;
        if (currentUserId.HasValue)
        {
            admin = await _unitOfWork.Admins.GetByIdAsync(currentUserId.Value);
        }

        admin ??= await _unitOfWork.Admins.GetFirstAdminAsync();
        if (admin is null) return null;

        return new AdminResponseDto(
            admin.Id,
            admin.Username,
            admin.FullName,
            admin.Email,
            admin.PhoneNumber,
            admin.CreatedAt
        );
    }

    public async Task<AdminResponseDto> UpdateAdminAsync(Guid? currentUserId, UpdateAdminDto dto)
    {
        AdminUser? admin = null;
        if (currentUserId.HasValue)
        {
            admin = await _unitOfWork.Admins.GetByIdAsync(currentUserId.Value);
        }

        admin ??= await _unitOfWork.Admins.GetFirstAdminAsync();
        if (admin is null)
        {
            throw new KeyNotFoundException("Yönetici profili bulunamadı.");
        }

        // Mevcut şifreyi doğrula (IPasswordHasher - SRP & DIP)
        if (!_passwordHasher.VerifyPasswordHash(dto.CurrentPassword, admin.PasswordHash, admin.PasswordSalt))
        {
            throw new InvalidOperationException("Güvenlik doğrulaması başarısız: Mevcut şifreniz hatalı.");
        }

        var newUsername = dto.Username.Trim().ToLowerInvariant();
        if (newUsername != admin.Username.ToLowerInvariant())
        {
            var exists = await _unitOfWork.Admins.AnyAsync(a => a.Id != admin.Id && a.Username.ToLower() == newUsername);
            if (exists)
            {
                throw new InvalidOperationException("Bu kullanıcı adı başka bir yönetici tarafından kullanılmaktadır.");
            }
            admin.Username = dto.Username.Trim();
        }

        admin.FullName = dto.FullName.Trim();
        admin.Email = dto.Email.Trim().ToLowerInvariant();
        admin.PhoneNumber = dto.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 6)
            {
                throw new InvalidOperationException("Yeni şifre en az 6 karakter olmalıdır.");
            }

            _passwordHasher.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
            admin.PasswordHash = hash;
            admin.PasswordSalt = salt;
            _logger.LogInformation("Yönetici şifresini güncelledi: {AdminId}", admin.Id);
        }

        _unitOfWork.Admins.Update(admin);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Yönetici profilini güncelledi: {AdminId}", admin.Id);

        return new AdminResponseDto(
            admin.Id,
            admin.Username,
            admin.FullName,
            admin.Email,
            admin.PhoneNumber,
            admin.CreatedAt
        );
    }

    public async Task<AdminResponseDto> CreateAdminAsync(CreateAdminDto dto)
    {
        var username = dto.Username.Trim().ToLowerInvariant();
        var exists = await _unitOfWork.Admins.AnyAsync(a => a.Username.ToLower() == username);
        if (exists)
        {
            throw new InvalidOperationException("Bu kullanıcı adına sahip bir yönetici zaten mevcut.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
        {
            throw new InvalidOperationException("Şifre en az 6 karakter olmalıdır.");
        }

        _passwordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

        var newAdmin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = dto.Username.Trim(),
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt
        };

        await _unitOfWork.Admins.AddAsync(newAdmin);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Yeni yönetici oluşturuldu: {AdminId} ({Username})", newAdmin.Id, newAdmin.Username);

        return new AdminResponseDto(
            newAdmin.Id,
            newAdmin.Username,
            newAdmin.FullName,
            newAdmin.Email,
            newAdmin.PhoneNumber,
            newAdmin.CreatedAt
        );
    }
}

