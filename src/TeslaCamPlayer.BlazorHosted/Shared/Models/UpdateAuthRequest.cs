namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class UpdateAuthRequest
{
    public bool IsEnabled { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string CurrentPassword { get; set; }
    public string FirstName { get; set; }
}
