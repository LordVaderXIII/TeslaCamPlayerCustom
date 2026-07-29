namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    [StringLength(50, ErrorMessage = "Username is too long.")]
    public string Username { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "Password is too long.")]
    public string Password { get; set; }
}
