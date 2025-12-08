using Serilog;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Server.Providers;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Is(LogEventLevel.Verbose)
	.WriteTo.Console()
	.CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddNewtonsoftJson();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ISettingsProvider, SettingsProvider>();
builder.Services.AddTransient<IClipsService, ClipsService>();
builder.Services.AddSingleton<IExportService, ExportService>();
#if WINDOWS
builder.Services.AddTransient<IFfProbeService, FfProbeServiceWindows>();
#elif DOCKER
builder.Services.AddTransient<IFfProbeService, FfProbeServiceDocker>();
#endif

builder.Services.AddDbContext<TeslaCamDbContext>((sp, options) =>
{
	var clipsRootPath = sp.GetService<ISettingsProvider>()!.Settings.ClipsRootPath;
	options.UseSqlite($"Data Source={Path.Combine(clipsRootPath, "teslacam.db")}");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
	dbContext.Database.EnsureCreated();
}

try
{
	var clipsRootPath = app.Services.GetService<ISettingsProvider>()!.Settings.ClipsRootPath;
	if (!Directory.Exists(clipsRootPath))
		throw new Exception("Configured clips root path doesn't exist, or no permission to access: " + clipsRootPath);
}
catch (Exception e)
{
	Log.Fatal(e, e.Message);
	return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseWebAssemblyDebugging();
}
else
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();