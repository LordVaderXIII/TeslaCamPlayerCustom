using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeslaCamPlayer.BlazorHosted.Server.Filters;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers;

[EnableRateLimiting("ApiPolicy")]
[TeslaCamAuth]
[ApiController]
[Route("Api/[action]")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpPost]
    public async Task<ActionResult<ExportJob>> StartExport([FromBody] ExportRequest request)
    {
        try
        {
            return await _exportService.StartExportAsync(request);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(429, ex.Message);
        }
    }

    [HttpGet]
    public IEnumerable<ExportJob> GetJobs()
    {
        return _exportService.GetJobs();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExportJob>> GetJob(Guid id)
    {
        var job = await _exportService.GetJobAsync(id);
        if (job == null) return NotFound();
        return job;
    }

    [HttpGet("{fileName}")]
    public IActionResult Download(string fileName)
    {
        // Sanitize input to prevent path traversal
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || sanitizedFileName != fileName)
        {
             return BadRequest("Invalid filename.");
        }

        var filePath = _exportService.GetExportFilePath(sanitizedFileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        return PhysicalFile(filePath, "video/mp4", sanitizedFileName);
    }
}
