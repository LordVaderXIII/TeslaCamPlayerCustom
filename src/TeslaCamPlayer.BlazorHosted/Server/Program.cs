using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Server.Middleware;
using TeslaCamPlayer.BlazorHosted.Server.Providers;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Is(LogEventLevel.Verbose)
	.WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
	.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Configure Forwarded Headers for Docker/Proxy environments
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust all proxies since the app is containerized and the proxy IP may change
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TeslaCamAuth";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression();
builder.Services.AddControllersWithViews().AddNewtonsoftJson();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ISettingsProvider, SettingsProvider>();
builder.Services.AddTransient<IClipsService, ClipsService>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<ISetupTokenService, SetupTokenService>();
builder.Services.AddTransient<IJulesApiService, JulesApiService>();
#if WINDOWS
builder.Services.AddTransient<IFfProbeService, FfProbeServiceWindows>();
#elif DOCKER
builder.Services.AddTransient<IFfProbeService, FfProbeServiceDocker>();
#endif

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(15)
            }));

    options.AddPolicy("LoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(15)
            }));
});

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
    // Ensure ExportJobs table exists if EnsureCreated didn't create it (because DB existed)
    try
    {
        dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ExportJobs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ExportJobs"" PRIMARY KEY,
                ""Name"" TEXT NULL,
                ""Status"" INTEGER NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""FileName"" TEXT NULL,
                ""ErrorMessage"" TEXT NULL,
                ""Progress"" REAL NOT NULL
            );
        ");

        dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY,
                ""Username"" TEXT NULL,
                ""PasswordHash"" TEXT NULL,
                ""FirstName"" TEXT NULL,
                ""IsEnabled"" INTEGER NOT NULL
            );
        ");

        var user = dbContext.Users.Find("Admin");
        if (user == null)
        {
            user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false, // Default off
                FirstName = "Admin"
            };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();
        }

        var resetAuth = Environment.GetEnvironmentVariable("RESET_AUTH");
        if (!string.IsNullOrEmpty(resetAuth) && bool.TryParse(resetAuth, out var shouldReset) && shouldReset)
        {
            user.IsEnabled = false;
            dbContext.Users.Update(user);
            dbContext.SaveChanges();
            Log.Information("Authentication reset to OFF via RESET_AUTH environment variable.");
        }

        if (!user.IsEnabled)
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ISetupTokenService>();
            var token = Guid.NewGuid().ToString("N");
            tokenService.Token = token;
            Log.Warning("Authentication is currently DISABLED. To enable it, use this Setup Token: {Token}", token);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to ensure database schema.");
    }
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
app.UseForwardedHeaders();

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

app.UseResponseCompression();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<JulesErrorReportingMiddleware>();

app.UseBlazorFrameworkFiles();

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".proto"] = "text/plain";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();