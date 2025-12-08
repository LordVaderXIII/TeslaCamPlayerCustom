using System;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class ExportJob
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ExportStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FileName { get; set; }
    public string ErrorMessage { get; set; }
    public double Progress { get; set; }
}
