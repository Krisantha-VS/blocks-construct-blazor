using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IBlocksApiGateway gateway) : ControllerBase
{
    [HttpPost("token")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest();
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var payload = form.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString()));

        var request = new HttpRequestMessage(HttpMethod.Post, "/idp/v1/Authentication/Token")
        {
            Content = new FormUrlEncodedContent(payload)
        };

        return await RelayAsync(request, cancellationToken);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest request, CancellationToken cancellationToken)
    {
        var body = new { email = request.Email };
        return await ForwardJsonAsync("/idp/v1/Iam/Recover", body, cancellationToken);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var body = new { code = request.Code, newPassword = request.NewPassword };
        return await ForwardJsonAsync("/idp/v1/Iam/ResetPassword", body, cancellationToken);
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request, CancellationToken cancellationToken)
    {
        var body = new { code = request.Code, password = request.Password };
        return await ForwardJsonAsync("/idp/v1/Iam/Activate", body, cancellationToken);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var body = new { refreshToken = request.RefreshToken };
        return await ForwardJsonAsync("/idp/v1/Authentication/Logout", body, cancellationToken);
    }

    private async Task<IActionResult> ForwardJsonAsync(string path, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = System.Net.Http.Json.JsonContent.Create(body)
        };

        return await RelayAsync(request, cancellationToken);
    }

    private async Task<IActionResult> RelayAsync(HttpRequestMessage outbound, CancellationToken cancellationToken)
    {
        var relay = await gateway.SendAsync(Request, outbound, cancellationToken);
        if (string.IsNullOrWhiteSpace(relay.Content))
        {
            return StatusCode(relay.StatusCode);
        }

        return new ContentResult
        {
            StatusCode = relay.StatusCode,
            Content = relay.Content,
            ContentType = relay.ContentType
        };
    }

    public class EmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ActivateRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string? RefreshToken { get; set; }
    }
}
