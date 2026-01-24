using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Microsoft.AspNetCore.Identity;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using UserModel = TeslaCamPlayer.BlazorHosted.Shared.Models.User;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly TeslaCamDbContext _dbContext;
    private readonly SetupTokenService _setupTokenService;

    public AuthController(TeslaCamDbContext dbContext, SetupTokenService setupTokenService)
    {
        _dbContext = dbContext;
        _setupTokenService = setupTokenService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var user = _dbContext.Users.Find("Admin");
        if (user == null) return NotFound();

        if (!user.IsEnabled)
        {
            return Ok(new AuthStatus
            {
                IsAuthenticated = true,
                Username = user.Username,
                FirstName = user.FirstName,
                AuthRequired = false,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
            });
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return Ok(new AuthStatus
            {
                IsAuthenticated = true,
                Username = User.Identity.Name,
                FirstName = user.FirstName,
                AuthRequired = true,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
            });
        }

        return Ok(new AuthStatus
        {
            IsAuthenticated = false,
            AuthRequired = true,
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
        });
    }

    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = _dbContext.Users.Find("Admin");
        if (user == null) return Unauthorized();

        if (!user.IsEnabled)
        {
            // If auth is disabled, allow login (though UI shouldn't really ask)
             return Ok();
        }

        // Simple hash check (should use a better hasher in production, but requirement says "simple" and we just need it "securely stored")
        // Using ASP.NET Identity PasswordHasher for security
        var hasher = new PasswordHasher<UserModel>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user.Username == request.Username && result == PasswordVerificationResult.Success)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("FirstName", user.FirstName ?? "Admin")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddDays(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return Ok();
        }

        return Unauthorized();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] UpdateAuthRequest request)
    {
        var user = _dbContext.Users.Find("Admin");
        if (user == null) return NotFound();

        // If auth is currently enabled, user must be authenticated to change settings
        if (user.IsEnabled && User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        // SECURITY: If a password is already set, we must verify it before allowing any changes,
        // even if auth is currently disabled (to prevent hijacking by enabling auth with a new password).
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                // For backward compatibility or if user forgot, we might want to check something else?
                // But strict security requires current password.
                // If user forgot password and auth is disabled, they should use RESET_AUTH env var which clears the password hash.
                return Unauthorized("Current password is required to change settings.");
            }

            var hasher = new PasswordHasher<UserModel>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (result == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Invalid current password.");
            }
        }
        else
        {
            // SECURITY: If no password is set (fresh install or reset), we require the Setup Token
            // to prevent unauthorized users from claiming the instance.
            if (request.CurrentPassword != _setupTokenService.Token)
            {
                return Unauthorized("Invalid Setup Token. Please check the application logs for the token.");
            }
        }

        user.IsEnabled = request.IsEnabled;
        user.FirstName = request.FirstName;
        user.Username = request.Username;

        if (!string.IsNullOrEmpty(request.Password))
        {
             var hasher = new PasswordHasher<UserModel>();
             user.PasswordHash = hasher.HashPassword(user, request.Password);
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        return Ok();
    }
}
