using MiniBank.DTOs;

namespace MiniBank.Services;

public interface IUserProfileService
{
    Task<MeResponseDto> GetMeAsync(Guid currentUserId);
    Task<MeResponseDto> UpdateProfileAsync(Guid currentUserId, UpdateProfileDto dto);
    Task ChangePasswordAsync(Guid currentUserId, ChangePasswordDto dto);
}

