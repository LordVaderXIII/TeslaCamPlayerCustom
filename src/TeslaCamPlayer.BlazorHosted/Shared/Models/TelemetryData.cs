using System.Text.Json.Serialization;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models
{
    public class TelemetryData
    {
        [JsonPropertyName("version")]
        public uint Version { get; set; }

        [JsonPropertyName("gearState")]
        public GearState Gear { get; set; }

        [JsonPropertyName("frameSeqNo")]
        public ulong FrameSeqNo { get; set; }

        [JsonPropertyName("vehicleSpeedMps")]
        public float VehicleSpeedMps { get; set; }

        [JsonPropertyName("acceleratorPedalPosition")]
        public float AcceleratorPedalPosition { get; set; }

        [JsonPropertyName("steeringWheelAngle")]
        public float SteeringWheelAngle { get; set; }

        [JsonPropertyName("blinkerOnLeft")]
        public bool BlinkerOnLeft { get; set; }

        [JsonPropertyName("blinkerOnRight")]
        public bool BlinkerOnRight { get; set; }

        [JsonPropertyName("brakeApplied")]
        public bool BrakeApplied { get; set; }

        [JsonPropertyName("autopilotState")]
        public AutopilotState AutopilotState { get; set; }

        [JsonPropertyName("latitudeDeg")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitudeDeg")]
        public double Longitude { get; set; }

        [JsonPropertyName("headingDeg")]
        public double Heading { get; set; }

        // Helper properties
        public double SpeedKmph => VehicleSpeedMps * 3.6;
        public double SpeedMph => VehicleSpeedMps * 2.23694;
    }

    public enum GearState
    {
        Park = 0,
        Drive = 1,
        Reverse = 2,
        Neutral = 3
    }

    public enum AutopilotState
    {
        None = 0,
        SelfDriving = 1,
        Autosteer = 2,
        Tacc = 3
    }
}
