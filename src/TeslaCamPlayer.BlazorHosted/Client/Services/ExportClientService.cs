using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TeslaCamPlayer.BlazorHosted.Client.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Client.Services;

public class ExportClientService : IExportClientService
{
    private readonly HttpClient _httpClient;

    public ExportClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExportJob> StartExportAsync(ExportRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("Api/StartExport", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportJob>();
    }

    public async Task<IEnumerable<ExportJob>> GetJobsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ExportJob>>("Api/GetJobs") ?? Enumerable.Empty<ExportJob>();
    }

    public async Task<ExportJob> GetJobAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<ExportJob>($"Api/GetJob/{id}");
    }
}
