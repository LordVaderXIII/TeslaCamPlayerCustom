namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class AuthStatus
{
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public bool AuthRequired { get; set; }
}
