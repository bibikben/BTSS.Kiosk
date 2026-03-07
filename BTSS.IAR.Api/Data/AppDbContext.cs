using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<SourceSystemEntity> SourceSystems => Set<SourceSystemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiClient>().HasIndex(x => x.ClientId).IsUnique();
        modelBuilder.Entity<ApiClient>().HasIndex(x => x.AgencyId);
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.GlobalSettingsJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.DeviceSettingsJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.DisplayRegistrationsJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.DeviceCommandsJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.AllowedScopesJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.ClientSecretHash)
            .HasMaxLength(256);
        modelBuilder.Entity<ApiClient>()
            .Property(x => x.ClientSecretSalt)
            .HasMaxLength(256);
        modelBuilder.Entity<ApiClient>()
            .HasOne(x => x.SourceSystem)
            .WithMany()
            .HasForeignKey(x => x.SourceSystemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SourceSystemEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SourceSystemEntity>().HasData(
            new SourceSystemEntity { Id = (short)SourceSystemCode.IAR, Code = "IAR", Description = "IAR JSON feed" },
            new SourceSystemEntity { Id = (short)SourceSystemCode.EmailText, Code = "EmailText", Description = "Email body parsing" }
        );
    }
}
