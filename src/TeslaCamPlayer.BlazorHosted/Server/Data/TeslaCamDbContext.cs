using Microsoft.EntityFrameworkCore;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Data;

public class TeslaCamDbContext : DbContext
{
	public DbSet<VideoFile> VideoFiles { get; set; }
	public DbSet<ExportJob> ExportJobs { get; set; }

	public TeslaCamDbContext(DbContextOptions<TeslaCamDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<VideoFile>()
			.HasKey(v => v.FilePath);

		modelBuilder.Entity<ExportJob>()
			.HasKey(j => j.Id);
	}
}
