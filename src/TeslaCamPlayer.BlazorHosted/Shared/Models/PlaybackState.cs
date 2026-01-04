using System.Text.Json.Serialization;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models
{
    public class PlaybackState
    {
        [JsonPropertyName("time")]
        public double Time { get; set; }

        [JsonPropertyName("telemetry")]
        public TelemetryData Telemetry { get; set; }
    }
}
