using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services
{
    public class JulesApiService : IJulesApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JulesApiService> _logger;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const string JulesApiUrl = "https://jules.googleapis.com/v1alpha/sessions";
        private const int DailyLimit = 5;
        private const string LimitFileName = "jules_sessions_limit.json";

        public JulesApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<JulesApiService> logger,
            ISettingsProvider settingsProvider,
            IWebHostEnvironment webHostEnvironment)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _settingsProvider = settingsProvider;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<JulesSessionResult> ReportErrorAsync(Exception ex, string contextInfo, string stackTrace = null)
        {
            // Backend errors are trusted, but still checked against ContentRootPath in IsSafeSourceFile
            return await ReportErrorInternalAsync(ex.Message, stackTrace ?? ex.StackTrace, contextInfo, skipSnippet: false);
        }

        public async Task<JulesSessionResult> ReportFrontendErrorAsync(string message, string stackTrace, string contextInfo)
        {
            // SECURITY: Frontend errors should not trigger snippet extraction as the stack trace is user-controlled.
            // Reading files based on frontend stack trace can lead to arbitrary file read vulnerabilities.
            return await ReportErrorInternalAsync(message, stackTrace, contextInfo, skipSnippet: true);
        }

        private async Task<JulesSessionResult> ReportErrorInternalAsync(string message, string stackTrace, string contextInfo, bool skipSnippet)
        {
            var apiKey = _configuration["JULES_API_KEY"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("JULES_API_KEY is not set. Skipping error reporting to Jules.");
                return new JulesSessionResult { IsSuccess = false, Message = "JULES_API_KEY is not set." };
            }

            var source = _configuration["JULES_SOURCE"];
            if (string.IsNullOrEmpty(source))
            {
                 _logger.LogWarning("JULES_SOURCE is not set. Skipping error reporting to Jules. Please set JULES_SOURCE to 'sources/github/OWNER/REPO'.");
                 return new JulesSessionResult { IsSuccess = false, Message = "JULES_SOURCE is not set." };
            }

            if (!await CheckAndIncrementDailyLimitAsync())
            {
                _logger.LogWarning("Daily limit for Jules sessions reached. Skipping error report.");
                return new JulesSessionResult { IsSuccess = false, Message = "Daily limit for Jules sessions reached." };
            }

            try
            {
                var prompt = BuildPrompt(message, stackTrace, contextInfo, skipSnippet);

                var requestBody = new
                {
                    prompt = prompt,
                    sourceContext = new
                    {
                        source = source,
                        githubRepoContext = new
                        {
                            startingBranch = "main" // Default to main, or make configurable
                        }
                    },
                    automationMode = "AUTO_CREATE_PR",
                    title = $"Bug Fix: {message.Substring(0, Math.Min(message.Length, 50))}"
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, JulesApiUrl);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Successfully created Jules session: {responseString}");
                    return new JulesSessionResult { IsSuccess = true, SessionResponse = responseString, Message = "Successfully created Jules session." };
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to create Jules session. Status: {response.StatusCode}, Error: {error}");
                    return new JulesSessionResult { IsSuccess = false, Error = error, Message = $"Failed to create Jules session. Status: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reporting to Jules API.");
                return new JulesSessionResult { IsSuccess = false, Message = "Exception while reporting to Jules API.", Error = ex.Message };
            }
        }

        private string BuildPrompt(string message, string stackTrace, string contextInfo, bool skipSnippet)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Analyze this error and generate a code patch to fix it. Focus on the C# backend if applicable.");
            sb.AppendLine();
            sb.AppendLine($"Error: {message}");
            sb.AppendLine($"Context: {contextInfo}");

            // App Version
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
            sb.AppendLine($"App Version: {version}");

            // Environment
            var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            sb.AppendLine($"Environment: {(isDocker ? "Docker" : "Standard")}");

            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(stackTrace);

            if (!skipSnippet)
            {
                var snippet = ExtractSnippet(stackTrace);
                if (!string.IsNullOrEmpty(snippet))
                {
                    sb.AppendLine();
                    sb.AppendLine("Code Snippet:");
                    sb.AppendLine(snippet);
                }
            }

            return sb.ToString();
        }

        private string ExtractSnippet(string stackTrace)
        {
            try
            {
                if (string.IsNullOrEmpty(stackTrace)) return null;
                var lines = stackTrace.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(":line "))
                    {
                        var parts = line.Split(new[] { ":line " }, StringSplitOptions.None);
                        if (parts.Length == 2 && int.TryParse(parts[1], out int lineNumber))
                        {
                            var filePart = parts[0];
                            var fileIndex = filePart.IndexOf(" in ");
                            if (fileIndex != -1)
                            {
                                var filePath = filePart.Substring(fileIndex + 4).Trim();
                                if (File.Exists(filePath) && IsSafeSourceFile(filePath))
                                {
                                    var fileLines = File.ReadAllLines(filePath);
                                    var start = Math.Max(0, lineNumber - 25);
                                    var end = Math.Min(fileLines.Length, lineNumber + 25);

                                    var snippet = new StringBuilder();
                                    snippet.AppendLine($"File: {filePath}");
                                    for (int i = start; i < end; i++)
                                    {
                                        snippet.AppendLine($"{i + 1}: {fileLines[i]}");
                                    }
                                    return snippet.ToString();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in snippet extraction
            }
            return null;
        }

        private bool IsSafeSourceFile(string filePath)
        {
            // Limit to code files to prevent arbitrary file read (e.g. /etc/passwd)
            var allowedExtensions = new[] { ".cs", ".razor", ".cshtml", ".js", ".ts", ".css", ".scss" };
            var ext = Path.GetExtension(filePath);
            if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // SECURITY: Ensure file is within ContentRootPath to prevent Path Traversal
            var fullPath = Path.GetFullPath(filePath);
            var contentRoot = Path.GetFullPath(_webHostEnvironment.ContentRootPath);
            // Ensure contentRoot ends with separator to prevent partial match (e.g. /var/www vs /var/www2)
            if (!contentRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                contentRoot += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CheckAndIncrementDailyLimitAsync()
        {
            try
            {
                var dataPath = _settingsProvider.Settings.ClipsRootPath;
                if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath))
                {
                     dataPath = "Data";
                     if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
                }

                var limitFile = Path.Combine(dataPath, LimitFileName);
                LimitData data = null;

                if (File.Exists(limitFile))
                {
                    var json = await File.ReadAllTextAsync(limitFile);
                    data = JsonConvert.DeserializeObject<LimitData>(json);
                }

                var today = DateTime.UtcNow.Date;

                if (data == null || data.Date != today)
                {
                    data = new LimitData { Date = today, Count = 0 };
                }

                if (data.Count >= DailyLimit)
                {
                    return false;
                }

                data.Count++;
                await File.WriteAllTextAsync(limitFile, JsonConvert.SerializeObject(data));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking daily limit.");
                return true;
            }
        }

        private class LimitData
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }
        }
    }
}
