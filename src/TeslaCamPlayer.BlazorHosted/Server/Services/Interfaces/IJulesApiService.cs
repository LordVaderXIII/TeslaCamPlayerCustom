using System;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces
{
    public interface IJulesApiService
    {
        Task<JulesSessionResult> ReportErrorAsync(Exception ex, string contextInfo, string stackTrace = null);
        Task<JulesSessionResult> ReportFrontendErrorAsync(string message, string stackTrace, string contextInfo);
    }
}
