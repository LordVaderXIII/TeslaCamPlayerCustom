namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

using System.ComponentModel.DataAnnotations;

public class UpdateAuthRequest
{
    public bool IsEnabled { get; set; }

    [StringLength(50, ErrorMessage = "Username is too long.")]
    public string Username { get; set; }

    [StringLength(100, ErrorMessage = "Password is too long.")]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "Current Password is too long.")]
    public string CurrentPassword { get; set; }

    [StringLength(100, ErrorMessage = "First Name is too long.")]
    public string FirstName { get; set; }
}
