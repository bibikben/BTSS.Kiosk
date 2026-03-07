using BTSS.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Data;

public sealed class ServiceDbContext(DbContextOptions<ServiceDbContext> options) : DbContext(options)
{
    public DbSet<IncidentSnapshotEntity> Incidents => Set<IncidentSnapshotEntity>();
    public DbSet<IncidentTransitionEntity> Transitions => Set<IncidentTransitionEntity>();
    public DbSet<PrintLedgerEntity> PrintLedger => Set<PrintLedgerEntity>();
    public DbSet<PollRunEntity> PollRuns => Set<PollRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentSnapshotEntity>(entity =>
        {
            entity.HasIndex(x => x.IncidentId).IsUnique();
            entity.Property(x => x.CanonicalJson).HasColumnType("TEXT");
            entity.Property(x => x.SummaryText).HasColumnType("TEXT");
        });

        modelBuilder.Entity<IncidentTransitionEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.ObservedAtUtc });
        });

        modelBuilder.Entity<PrintLedgerEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.TemplateKind, x.SummaryHash }).IsUnique();
            entity.HasIndex(x => new { x.PrintedAtUtc, x.Status });
            entity.Property(x => x.TemplateKind).HasMaxLength(64);
            entity.Property(x => x.PrinterName).HasMaxLength(256);
            entity.Property(x => x.PayloadSummary).HasColumnType("TEXT");
            entity.Property(x => x.Error).HasColumnType("TEXT");
        });

        modelBuilder.Entity<PollRunEntity>(entity =>
        {
            entity.HasIndex(x => x.StartedAtUtc);
            entity.Property(x => x.Error).HasColumnType("TEXT");
        });
    }
}
