using System.ComponentModel.DataAnnotations;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class UpdateAuthRequest
{
    public bool IsEnabled { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; }

    [StringLength(100)]
    public string Password { get; set; }

    [StringLength(100)]
    public string CurrentPassword { get; set; }

    [StringLength(50)]
    public string FirstName { get; set; }
}
