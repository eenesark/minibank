using MiniBank.DTOs;

namespace MiniBank.Services;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<IEnumerable<AdminResponseDto>> GetAllAdminsAsync();
    Task<AdminResponseDto?> GetAdminByIdAsync(Guid id);
    Task<AdminResponseDto?> GetMeAsync(Guid? currentUserId);
    Task<AdminResponseDto> UpdateAdminAsync(Guid? currentUserId, UpdateAdminDto dto);
    Task<AdminResponseDto> CreateAdminAsync(CreateAdminDto dto);
}

