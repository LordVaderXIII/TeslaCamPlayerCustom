using System;
using System.Threading.Tasks;

namespace TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces
{
    public interface IJulesApiService
    {
        Task ReportErrorAsync(Exception ex, string contextInfo, string stackTrace = null);
        Task ReportFrontendErrorAsync(string message, string stackTrace, string contextInfo);
    }
}
