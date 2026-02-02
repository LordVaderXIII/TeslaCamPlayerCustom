using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeslaCamPlayer.BlazorHosted.Server.Filters;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers
{
    [EnableRateLimiting("LogsPolicy")]
    [TeslaCamAuth]
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly IJulesApiService _julesApiService;

        public LogsController(IJulesApiService julesApiService)
        {
            _julesApiService = julesApiService;
        }

        [HttpGet]
        public async Task<ActionResult<List<LogEntry>>> GetLogs([FromQuery] int count = 1000)
        {
            // Security: Limit count to prevent memory exhaustion (DoS)
            if (count > 5000) count = 5000;
            if (count < 1) count = 100;

            var logs = new Queue<LogEntry>();
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

            if (!Directory.Exists(logsDir))
            {
                return Ok(new List<LogEntry>());
            }

            // Get today's log file
            var today = DateTime.Now.ToString("yyyyMMdd");
            var logFile = Directory.GetFiles(logsDir, $"log-{today}.txt").FirstOrDefault();

            // If no log file for today, maybe try recent ones? For now, just today.
            if (logFile == null || !System.IO.File.Exists(logFile))
            {
                // Fallback to finding any log file if today's is missing (rare)
                logFile = Directory.GetFiles(logsDir, "log-*.txt").OrderByDescending(f => f).FirstOrDefault();
            }

            if (logFile != null)
            {
                using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    LogEntry currentEntry = null;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // Parse Serilog default format: 2025-12-09 12:00:00.000 +00:00 [INF] Message
                        // Or if it's an exception stack trace (indented)

                        if (DateTime.TryParse(line.Split(' ')[0], out var date))
                        {
                            if (currentEntry != null)
                            {
                                logs.Enqueue(currentEntry);
                                if (logs.Count > count) logs.Dequeue();
                            }

                            currentEntry = new LogEntry
                            {
                                RawLine = line,
                                Timestamp = date,
                                Message = line
                            };

                            if (line.Contains("[ERR]") || line.Contains("[FTL]"))
                            {
                                currentEntry.Level = "Error";
                                currentEntry.IsCandidate = true;
                            }
                            else if (line.Contains("[WRN]"))
                            {
                                currentEntry.Level = "Warning";
                            }
                            else
                            {
                                currentEntry.Level = "Info";
                            }
                        }
                        else
                        {
                            // Continuation of previous entry (e.g. stack trace)
                            if (currentEntry != null)
                            {
                                currentEntry.RawLine += Environment.NewLine + line;
                                currentEntry.Message += Environment.NewLine + line;
                                if (currentEntry.IsCandidate)
                                {
                                    currentEntry.Exception += line + Environment.NewLine;
                                    currentEntry.StackTrace += line + Environment.NewLine;
                                }
                            }
                        }
                    }
                    if (currentEntry != null)
                    {
                        logs.Enqueue(currentEntry);
                        if (logs.Count > count) logs.Dequeue();
                    }
                }
            }

            // Reverse to show newest first
            return Ok(logs.Reverse().ToList());
        }

        [HttpPost("report")]
        public async Task<ActionResult<JulesSessionResult>> ReportError([FromBody] LogEntry entry)
        {
            if (entry == null) return BadRequest("Invalid log entry");

            var result = await _julesApiService.ReportErrorAsync(new Exception(entry.Message), "Manual Report from Logs", entry.StackTrace ?? entry.RawLine);
            return Ok(result);
        }
    }
}
