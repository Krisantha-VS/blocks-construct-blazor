using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

/// <summary>
/// Demonstrates all supported access levels.
/// Each endpoint intentionally has no authorization applied yet.
/// The SELISE auth team should apply the indicated attribute/policy to each endpoint.
/// </summary>
[ApiController]
[Route("api/auth-showcase")]
[Tags("Auth Showcase")]
public class AuthShowcaseController : ControllerBase
{
    // -------------------------------------------------------------------------
    // LEVEL 1: PUBLIC
    // No authentication required. Anyone can call this endpoint.
    //
    // TO IMPLEMENT: Add [AllowAnonymous] attribute
    // -------------------------------------------------------------------------
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new
        {
            level = "Public",
            message = "This endpoint is open to everyone. No token needed.",
            data = new { appName = "Blocks Construct", version = "1.0.0" }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 2: AUTHENTICATED
    // User must be logged in. Any valid user with a token can access this.
    // Role does not matter — just needs a valid JWT.
    //
    // TO IMPLEMENT: Add [Authorize] attribute
    // -------------------------------------------------------------------------
    [HttpGet("authenticated")]
    public IActionResult AuthenticatedEndpoint()
    {
        return Ok(new
        {
            level = "Authenticated",
            message = "This endpoint requires a valid login token.",
            data = new { dashboard = "user-dashboard", items = 42 }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 3: ROLE-BASED — "admin" role
    // User must be logged in AND have the "admin" role assigned.
    //
    // TO IMPLEMENT: Add [Authorize(Roles = "admin")] attribute
    // -------------------------------------------------------------------------
    [HttpGet("admin-only")]
    public IActionResult AdminOnlyEndpoint()
    {
        return Ok(new
        {
            level = "Role-based (admin)",
            message = "This endpoint is restricted to users with the 'admin' role.",
            data = new { totalUsers = 320, systemHealth = "OK" }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 4: ROLE-BASED — "manager" role
    // User must be logged in AND have the "manager" role assigned.
    //
    // TO IMPLEMENT: Add [Authorize(Roles = "manager")] attribute
    // -------------------------------------------------------------------------
    [HttpGet("manager-only")]
    public IActionResult ManagerOnlyEndpoint()
    {
        return Ok(new
        {
            level = "Role-based (manager)",
            message = "This endpoint is restricted to users with the 'manager' role.",
            data = new { teamSize = 12, pendingApprovals = 5 }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 5: PERMISSION-BASED
    // User must be logged in AND have the "reports:read" permission.
    // More granular than roles — a user can have this permission via any role.
    //
    // TO IMPLEMENT:
    //   1. Register a policy in Program.cs:
    //      builder.Services.AddAuthorization(options =>
    //          options.AddPolicy("reports:read", policy =>
    //              policy.RequireClaim("permission", "reports:read")));
    //   2. Add [Authorize(Policy = "reports:read")] attribute here
    // -------------------------------------------------------------------------
    [HttpGet("permission-based")]
    public IActionResult PermissionBasedEndpoint()
    {
        return Ok(new
        {
            level = "Permission-based (reports:read)",
            message = "This endpoint requires the 'reports:read' permission.",
            data = new { reportCount = 15, lastGenerated = DateTime.UtcNow }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 6: OWNER-ONLY
    // User must be logged in AND can only access their own resource.
    // The userId in the route must match the authenticated user's identity.
    //
    // TO IMPLEMENT:
    //   1. Add [Authorize] attribute
    //   2. Inside the action, validate:
    //      if (userId.ToString() != User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
    //          return Forbid();
    // -------------------------------------------------------------------------
    [HttpGet("owner-only/{userId}")]
    public IActionResult OwnerOnlyEndpoint(string userId)
    {
        return Ok(new
        {
            level = "Owner-only",
            message = $"This endpoint returns data only if the caller owns userId: {userId}.",
            data = new { userId, email = "owner@example.com", preferences = new { theme = "dark" } }
        });
    }

    // -------------------------------------------------------------------------
    // LEVEL 7: SERVICE-TO-SERVICE
    // Not called by human users. Called by other backend services using an API key
    // or OAuth 2.0 Client Credentials flow (no user context).
    //
    // TO IMPLEMENT:
    //   Option A — API Key: validate a custom header (e.g. X-Api-Key) in middleware
    //   Option B — Client Credentials: configure JWT with client_credentials grant
    //      and add [Authorize(AuthenticationSchemes = "ClientCredentials")]
    // -------------------------------------------------------------------------
    [HttpGet("service-to-service")]
    public IActionResult ServiceToServiceEndpoint()
    {
        return Ok(new
        {
            level = "Service-to-service",
            message = "This endpoint is for internal backend services only. No human user context.",
            data = new { syncStatus = "OK", lastSync = DateTime.UtcNow }
        });
    }
}
