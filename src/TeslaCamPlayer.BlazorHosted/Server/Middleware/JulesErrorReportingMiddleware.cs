using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Middleware
{
    public class JulesErrorReportingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JulesErrorReportingMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        public JulesErrorReportingMiddleware(RequestDelegate next, ILogger<JulesErrorReportingMiddleware> logger, IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception caught by middleware.");

                try
                {
                    // Resolve scoped service within the scope of the request if possible, or create a scope?
                    // JulesApiService is likely Scoped or Transient.
                    // Wait, JulesApiService relies on ISettingsProvider which is Singleton, and HttpClient.
                    // Let's check Program.cs. I haven't registered it yet. I will register it as Transient or Scoped.
                    // Since I'm in middleware, I should resolve it from context.RequestServices.

                    var julesService = context.RequestServices.GetService(typeof(IJulesApiService)) as IJulesApiService;
                    if (julesService != null)
                    {
                        await julesService.ReportErrorAsync(ex, $"Unhandled Exception in Middleware. Request Path: {context.Request.Path}");
                    }
                }
                catch (Exception reportEx)
                {
                    _logger.LogError(reportEx, "Failed to report error to Jules.");
                }

                throw; // Re-throw to let standard error handling (e.g. developer exception page) work if enabled
            }
        }
    }
}
