using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Client.Services
{
    public interface ITelemetryService
    {
        Task<bool> InitAsync(string videoUrl);
        Task<TelemetryData> GetTelemetryAsync(double timeSeconds);
        Task<TelemetryResult> GetTelemetryForVideoAsync(ElementReference videoElement);
        Task<System.Collections.Generic.List<double[]>> GetPathAsync();
    }

    public class TelemetryResult
    {
        [JsonPropertyName("time")]
        public double Time { get; set; }

        [JsonPropertyName("telemetry")]
        public TelemetryData Telemetry { get; set; }
    }

    public class TelemetryService : ITelemetryService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private IJSObjectReference _module;

        public TelemetryService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<bool> InitAsync(string videoUrl)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("telemetryInterop.init", videoUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telemetry Init Failed: {ex.Message}");
                return false;
            }
        }

        public async Task<TelemetryData> GetTelemetryAsync(double timeSeconds)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<TelemetryData>("telemetryInterop.getTelemetry", timeSeconds);
            }
            catch
            {
                return null;
            }
        }

        public async Task<TelemetryResult> GetTelemetryForVideoAsync(ElementReference videoElement)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<TelemetryResult>("telemetryInterop.getTelemetryForVideo", videoElement);
            }
            catch
            {
                // Fallback or error
                return new TelemetryResult { Time = 0, Telemetry = null };
            }
        }

        public async Task<System.Collections.Generic.List<double[]>> GetPathAsync()
        {
             try
             {
                 return await _jsRuntime.InvokeAsync<System.Collections.Generic.List<double[]>>("telemetryInterop.getPath");
             }
             catch
             {
                 return new System.Collections.Generic.List<double[]>();
             }
        }

        public async ValueTask DisposeAsync()
        {
             // Cleanup if needed
        }
    }
}
