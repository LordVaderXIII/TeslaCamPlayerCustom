using System;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public string StackTrace { get; set; }
        public bool IsCandidate { get; set; }
        public string RawLine { get; set; }
    }
}
