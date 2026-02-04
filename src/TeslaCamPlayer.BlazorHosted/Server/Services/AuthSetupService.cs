using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public class AuthSetupService : IAuthSetupService
{
    public string SetupToken { get; } = Guid.NewGuid().ToString();
}
