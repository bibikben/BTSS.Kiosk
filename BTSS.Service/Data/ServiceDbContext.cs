using BTSS.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Data;

public sealed class ServiceDbContext(DbContextOptions<ServiceDbContext> options) : DbContext(options)
{
    public DbSet<IncidentSnapshotEntity> Incidents => Set<IncidentSnapshotEntity>();
    public DbSet<IncidentTransitionEntity> Transitions => Set<IncidentTransitionEntity>();
    public DbSet<PrintLedgerEntity> PrintLedger => Set<PrintLedgerEntity>();
    public DbSet<PollRunEntity> PollRuns => Set<PollRunEntity>();
    public DbSet<LocalIncidentCommentEntity> LocalIncidentComments => Set<LocalIncidentCommentEntity>();
    public DbSet<LocalIncidentUnitEntity> LocalIncidentUnits => Set<LocalIncidentUnitEntity>();
    public DbSet<LocalUnitTimelineFactEntity> LocalUnitTimelineFacts => Set<LocalUnitTimelineFactEntity>();
    public DbSet<LocalSyncStateEntity> LocalSyncStates => Set<LocalSyncStateEntity>();
    public DbSet<LocalOutboxEntity> LocalOutbox => Set<LocalOutboxEntity>();
    public DbSet<SftpImportFileEntity> SftpImportFiles => Set<SftpImportFileEntity>();
    public DbSet<SftpImportIncidentEntity> SftpImportIncidents => Set<SftpImportIncidentEntity>();

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

        modelBuilder.Entity<LocalIncidentCommentEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.OccurredAtUtc, x.Message });
            entity.Property(x => x.Message).HasColumnType("TEXT");
            entity.Property(x => x.CreatedBy).HasMaxLength(128);
            entity.Property(x => x.CreatedAgency).HasMaxLength(128);
        });

        modelBuilder.Entity<LocalIncidentUnitEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.UnitIdentifier, x.AgencyId }).IsUnique();
            entity.Property(x => x.UnitIdentifier).HasMaxLength(128);
            entity.Property(x => x.Station).HasMaxLength(128);
            entity.Property(x => x.UnitType).HasMaxLength(128);
            entity.Property(x => x.CurrentStatus).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalUnitTimelineFactEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.UnitIdentifier, x.AgencyId }).IsUnique();
            entity.Property(x => x.UnitIdentifier).HasMaxLength(128);
        });

        modelBuilder.Entity<LocalSyncStateEntity>(entity =>
        {
            entity.HasIndex(x => x.DeviceId).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.LastBatchId).HasMaxLength(64);
            entity.Property(x => x.ConflictPolicy).HasMaxLength(64);
        });

        modelBuilder.Entity<LocalOutboxEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.CreatedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Scope).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64);
            entity.Property(x => x.Message).HasColumnType("TEXT");
            entity.Property(x => x.PayloadJson).HasColumnType("TEXT");
        });
        modelBuilder.Entity<SftpImportFileEntity>(entity =>
        {
            entity.HasIndex(x => x.RemotePath).IsUnique();
            entity.HasIndex(x => new { x.Status, x.LastSeenUtc });
            entity.Property(x => x.RemotePath).HasMaxLength(1024);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Error).HasColumnType("TEXT");
        });
        modelBuilder.Entity<SftpImportIncidentEntity>(entity =>
        {
            entity.HasIndex(x => new { x.SftpImportFileId, x.IncidentId }).IsUnique();
            entity.Property(x => x.IncidentId).HasMaxLength(128);
            entity.Property(x => x.Error).HasColumnType("TEXT");
        });
    }
}
