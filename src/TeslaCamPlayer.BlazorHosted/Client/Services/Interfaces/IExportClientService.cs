using System.Collections.Generic;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Client.Services.Interfaces;

public interface IExportClientService
{
    Task<ExportJob> StartExportAsync(ExportRequest request);
    Task<IEnumerable<ExportJob>> GetJobsAsync();
    Task<ExportJob> GetJobAsync(System.Guid id);
}
