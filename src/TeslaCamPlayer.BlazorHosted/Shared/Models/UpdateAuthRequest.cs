using System.ComponentModel.DataAnnotations;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class UpdateAuthRequest
{
    public bool IsEnabled { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; }

    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; }

    public string CurrentPassword { get; set; }

    [StringLength(50)]
    public string FirstName { get; set; }
}
