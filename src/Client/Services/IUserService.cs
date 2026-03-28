using Client.Models.IAM;
using Client.Models.Profile;

namespace Client.Services;

public interface IUserService
{
    Task<PagedResult<IamUser>> GetUsersAsync(int page, int pageSize, string? email = null, string? name = null);
    Task<UserProfile?> GetCurrentProfileAsync();
    Task InviteUserAsync(AddUserRequest request);
    Task SendPasswordResetAsync(string email);
    Task ResendActivationAsync(string email);
    Task UpdateProfileAsync(string userId, string firstName, string? lastName, string? phoneNumber);
    Task ChangePasswordAsync(string currentPassword, string newPassword);
}
