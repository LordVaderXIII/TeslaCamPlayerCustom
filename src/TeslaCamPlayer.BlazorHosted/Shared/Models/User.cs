using System.ComponentModel.DataAnnotations;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class User
{
    [Key]
    public string Id { get; set; } = "Admin";
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public bool IsEnabled { get; set; }
}
