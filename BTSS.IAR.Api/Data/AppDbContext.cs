using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CallRecordEntity> Calls => Set<CallRecordEntity>();
    public DbSet<CallUnitEntity> CallUnits => Set<CallUnitEntity>();
    public DbSet<PollState> PollStates => Set<PollState>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<SourceSystemEntity> SourceSystems => Set<SourceSystemEntity>();
    public DbSet<PriorityEntity> Priorities => Set<PriorityEntity>();
    public DbSet<CallTypeEntity> CallTypes => Set<CallTypeEntity>();
    public DbSet<CadAgencyEntity> CadAgencies => Set<CadAgencyEntity>();
    public DbSet<CallStatusEntity> CallStatuses => Set<CallStatusEntity>();
    public DbSet<UnitStatusEntity> UnitStatuses => Set<UnitStatusEntity>();
    public DbSet<UnitEntity> Units => Set<UnitEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CallRecordEntity>()
            .HasIndex(x => new { x.AgencyId, x.CallIdentifier })
            .IsUnique();

        modelBuilder.Entity<CallRecordEntity>()
            .HasOne(x => x.SourceSystemLookup)
            .WithMany()
            .HasForeignKey(x => x.SourceSystemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CallUnitEntity>()
            .HasOne(x => x.CallRecord)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.CallRecordId);

        modelBuilder.Entity<CallUnitEntity>()
            .HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CallUnitEntity>()
            .HasOne(x => x.StatusOriginal)
            .WithMany()
            .HasForeignKey(x => x.StatusOriginalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApiClient>()
            .HasIndex(x => x.ClientId)
            .IsUnique();

        // Lookup uniqueness
        modelBuilder.Entity<SourceSystemEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<PriorityEntity>().HasIndex(x => x.Value).IsUnique();
        modelBuilder.Entity<CallTypeEntity>().HasIndex(x => x.Value).IsUnique();
        modelBuilder.Entity<CadAgencyEntity>().HasIndex(x => x.ExternalKey).IsUnique();
        modelBuilder.Entity<CallStatusEntity>().HasIndex(x => x.Value).IsUnique();
        modelBuilder.Entity<UnitStatusEntity>().HasIndex(x => x.Value).IsUnique();
        modelBuilder.Entity<UnitEntity>().HasIndex(x => x.ExternalKey).IsUnique();

        // Seed source systems to keep enum mapping stable
        modelBuilder.Entity<SourceSystemEntity>().HasData(
            new SourceSystemEntity { Id = (short)SourceSystemCode.IAR, Code = "IAR", Description = "IAR JSON feed" },
            new SourceSystemEntity { Id = (short)SourceSystemCode.EmailText, Code = "EmailText", Description = "Email body parsing" }
        );
    }
}
