using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/iam")]
public class IamController(IBlocksApiGateway gateway) : ControllerBase
{
    [HttpPost("users/search")]
    public async Task<IActionResult> SearchUsers([FromBody] SearchUsersRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            page = request.Page,
            pageSize = request.PageSize,
            sort = request.Sort,
            filter = request.Filter
        };

        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/GetUsers", body, cancellationToken);
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        return await ForwardAsync(HttpMethod.Get, "/idp/v1/Iam/GetAccount", cancellationToken);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] string userId, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["filter.userId"] = userId
        };

        var queryString = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return await ForwardAsync(HttpMethod.Get, $"/idp/v1/Iam/GetSessions?{queryString}", cancellationToken);
    }

    [HttpPost("users/invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName ?? string.Empty,
            phoneNumber = request.PhoneNumber ?? string.Empty,
            language = "en",
            userPassType = "Plain",
            password = string.Empty,
            mfaEnabled = false,
            allowedLogInType = new[] { "Email" }
        };

        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/Create", body, cancellationToken);
    }

    [HttpPost("users/recover")]
    public async Task<IActionResult> Recover([FromBody] EmailRequest request, CancellationToken cancellationToken)
    {
        var body = new { email = request.Email };
        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/Recover", body, cancellationToken);
    }

    [HttpPost("users/resend-activation")]
    public async Task<IActionResult> ResendActivation([FromBody] EmailRequest request, CancellationToken cancellationToken)
    {
        var body = new { email = request.Email };
        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/ResendActivation", body, cancellationToken);
    }

    [HttpPost("users/update")]
    public async Task<IActionResult> Update([FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            userId = request.UserId,
            firstName = request.FirstName,
            lastName = request.LastName ?? string.Empty,
            phoneNumber = request.PhoneNumber ?? string.Empty
        };

        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/Update", body, cancellationToken);
    }

    [HttpPost("users/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            oldPassword = request.OldPassword,
            newPassword = request.NewPassword
        };

        return await ForwardJsonAsync(HttpMethod.Post, "/idp/v1/Iam/ChangePassword", body, cancellationToken);
    }

    private async Task<IActionResult> ForwardJsonAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = System.Net.Http.Json.JsonContent.Create(body)
        };

        return await RelayAsync(request, cancellationToken, forwardAuthorization: true);
    }

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        return await RelayAsync(request, cancellationToken, forwardAuthorization: true);
    }

    private async Task<IActionResult> RelayAsync(HttpRequestMessage outbound, CancellationToken cancellationToken, bool forwardAuthorization)
    {
        var relay = await gateway.SendAsync(Request, outbound, cancellationToken, forwardAuthorization);
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

    public class SearchUsersRequest
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public object? Sort { get; set; }
        public object? Filter { get; set; }
    }

    public class InviteUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class EmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
