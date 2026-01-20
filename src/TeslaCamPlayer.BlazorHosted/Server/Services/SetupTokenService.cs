using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public class SetupTokenService : ISetupTokenService
{
    public string? Token { get; set; }
}
