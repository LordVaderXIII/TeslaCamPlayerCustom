using System.Web;
using Microsoft.AspNetCore.Mvc;
using TeslaCamPlayer.BlazorHosted.Server.Filters;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Controllers;

[TeslaCamAuth]
[ApiController]
[Route("Api/[action]")]
public class ApiController : ControllerBase
{
	private readonly IClipsService _clipsService;
	private readonly string _rootFullPath;

	public ApiController(ISettingsProvider settingsProvider, IClipsService clipsService)
	{
		_rootFullPath = Path.GetFullPath(settingsProvider.Settings.ClipsRootPath);
		_clipsService = clipsService;
	}

	[HttpGet]
	public async Task<Clip[]> GetClips(SyncMode syncMode = SyncMode.None)
		=> await _clipsService.GetClipsAsync(syncMode);

	private bool IsUnderRootPath(string path)
	{
		var root = _rootFullPath.EndsWith(Path.DirectorySeparatorChar)
			? _rootFullPath
			: _rootFullPath + Path.DirectorySeparatorChar;
		return path.StartsWith(root, StringComparison.Ordinal);
	}

	[HttpGet("{path}.mp4")]
	public IActionResult Video(string path)
		=> ServeFile(path, ".mp4", "video/mp4", true);

	[HttpGet("{path}.png")]
	public IActionResult Thumbnail(string path)
		=> ServeFile(path, ".png", "image/png");

	private IActionResult ServeFile(string path, string extension, string contentType, bool enableRangeProcessing = false)
	{
		path = HttpUtility.UrlDecode(path);
		path += extension;

		path = Path.GetFullPath(path);
		if (!IsUnderRootPath(path))
			return BadRequest("File must be in subdirectory under the clips root path.");

		if (!System.IO.File.Exists(path))
			return NotFound();

		return PhysicalFile(path, contentType, enableRangeProcessing);
	}
}