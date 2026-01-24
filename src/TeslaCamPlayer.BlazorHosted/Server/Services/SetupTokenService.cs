using System;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public class SetupTokenService
{
    public string Token { get; set; }

    public string GenerateToken()
    {
        Token = Guid.NewGuid().ToString("N");
        return Token;
    }
}
