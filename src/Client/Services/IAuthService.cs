using Client.Models.Auth;

namespace Client.Services;

public interface IAuthService
{
    Task<SignInResponse> SignInAsync(string username, string password);
    Task<SignInResponse> VerifyMfaAsync(MfaVerifyRequest request);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task SetPasswordAsync(SetPasswordRequest request);
    Task SignOutAsync();
    Task<string?> GetAccessTokenAsync();
}
