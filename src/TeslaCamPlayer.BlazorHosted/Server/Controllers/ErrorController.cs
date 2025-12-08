using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers
{
    [ApiController]
    [Route("Api/[action]")]
    public class ErrorController : ControllerBase
    {
        private readonly IJulesApiService _julesApiService;

        public ErrorController(IJulesApiService julesApiService)
        {
            _julesApiService = julesApiService;
        }

        [HttpPost]
        public async Task<IActionResult> ReportError([FromBody] ErrorReportRequest request)
        {
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
