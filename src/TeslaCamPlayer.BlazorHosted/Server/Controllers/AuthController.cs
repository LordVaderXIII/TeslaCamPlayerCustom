using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Microsoft.AspNetCore.Identity;
using UserModel = TeslaCamPlayer.BlazorHosted.Shared.Models.User;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly TeslaCamDbContext _dbContext;

    public AuthController(TeslaCamDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var user = _dbContext.Users.Find("Admin");
        if (user == null) return NotFound();

        if (!user.IsEnabled)
        {
            // If auth is disabled, automatically sign in the user to establish a session.
            // This ensures that even in "Trust" mode, we have a CSRF-protected session for sensitive operations.
            if (User.Identity?.IsAuthenticated != true)
            {
                await SignInUserAsync(user);
            }

            return Ok(new AuthStatus
            {
                IsAuthenticated = true,
                Username = user.Username,
                FirstName = user.FirstName,
                AuthRequired = false
            });
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return Ok(new AuthStatus
            {
                IsAuthenticated = true,
                Username = User.Identity.Name,
                FirstName = user.FirstName,
                AuthRequired = true
            });
        }

        return Ok(new AuthStatus
        {
            IsAuthenticated = false,
            AuthRequired = true
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
            // If auth is disabled, allow login and establish session
            await SignInUserAsync(user);
            return Ok();
        }

        // Simple hash check (should use a better hasher in production, but requirement says "simple" and we just need it "securely stored")
        // Using ASP.NET Identity PasswordHasher for security
        var hasher = new PasswordHasher<UserModel>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user.Username == request.Username && result == PasswordVerificationResult.Success)
        {
            await SignInUserAsync(user);
            return Ok();
        }

        return Unauthorized();
    }

    private async Task SignInUserAsync(UserModel user)
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

        // User must be authenticated to change settings (even if auth is disabled, they should have an auto-session)
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
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
