using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TeslaCamPlayer.BlazorHosted.Client;
using TeslaCamPlayer.BlazorHosted.Client.Services;
using TeslaCamPlayer.BlazorHosted.Client.Services.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IExportClientService, ExportClientService>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();