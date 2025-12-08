using System.Collections.Generic;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

public interface IExportService
{
    Task<ExportJob> StartExportAsync(ExportRequest request);
    IEnumerable<ExportJob> GetJobs();
    Task<ExportJob> GetJobAsync(System.Guid id);
    string GetExportFilePath(string fileName);
}
