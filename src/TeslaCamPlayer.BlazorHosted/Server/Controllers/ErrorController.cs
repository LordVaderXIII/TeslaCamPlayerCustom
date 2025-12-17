using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers
{
    [ApiController]
    [Route("Api/[action]")]
    public class ErrorController : ControllerBase
    {
        private readonly IJulesApiService _julesApiService;
        private readonly IMemoryCache _memoryCache;

        public ErrorController(IJulesApiService julesApiService, IMemoryCache memoryCache)
        {
            _julesApiService = julesApiService;
            _memoryCache = memoryCache;
        }

        [HttpPost]
        public async Task<IActionResult> ReportError([FromBody] ErrorReportRequest request)
        {
            if (request == null) return BadRequest("Invalid request body.");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var cacheKey = $"ErrorReport_{ip}";

            if (_memoryCache.TryGetValue(cacheKey, out int count))
            {
                if (count >= 20)
                {
                    return StatusCode(429, "Too many error reports. Please try again later.");
                }
                _memoryCache.Set(cacheKey, count + 1, TimeSpan.FromHours(1));
            }
            else
            {
                _memoryCache.Set(cacheKey, 1, TimeSpan.FromHours(1));
            }

            await _julesApiService.ReportFrontendErrorAsync(request.Message, request.StackTrace, request.ContextInfo);
            return Ok();
        }
    }

    public class ErrorReportRequest
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string ContextInfo { get; set; }
    }
}
