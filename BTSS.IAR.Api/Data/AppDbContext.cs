using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<SourceSystemEntity> SourceSystems => Set<SourceSystemEntity>();
    public DbSet<IngestedIncident> IngestedIncidents => Set<IngestedIncident>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<RoleDefinition> Roles => Set<RoleDefinition>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<UserAgencyMembership> UserAgencies => Set<UserAgencyMembership>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<RolePermissionAssignment> RolePermissions => Set<RolePermissionAssignment>();
    public DbSet<UserPermissionOverride> UserPermissions => Set<UserPermissionOverride>();
    public DbSet<IncidentEntity> Incidents => Set<IncidentEntity>();
    public DbSet<IncidentAgencyEntity> IncidentAgencies => Set<IncidentAgencyEntity>();
    public DbSet<IncidentCallerEntity> IncidentCallers => Set<IncidentCallerEntity>();
    public DbSet<IncidentCommentEntity> IncidentComments => Set<IncidentCommentEntity>();
    public DbSet<IncidentUnitEntity> IncidentUnits => Set<IncidentUnitEntity>();
    public DbSet<UnitStatusEventEntity> UnitStatusEvents => Set<UnitStatusEventEntity>();
    public DbSet<UnitTimelineFactEntity> UnitTimelineFacts => Set<UnitTimelineFactEntity>();
    public DbSet<IncidentAssignmentEntity> IncidentAssignments => Set<IncidentAssignmentEntity>();
    public DbSet<IncidentSyncLogEntity> IncidentSyncLog => Set<IncidentSyncLogEntity>();
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<DeviceAgencyEntity> DeviceAgencies => Set<DeviceAgencyEntity>();
    public DbSet<DeviceSettingEntity> DeviceSettings => Set<DeviceSettingEntity>();
    public DbSet<GlobalSettingEntity> GlobalSettings => Set<GlobalSettingEntity>();
    public DbSet<DeviceHeartbeatEntity> DeviceHeartbeats => Set<DeviceHeartbeatEntity>();
    public DbSet<DeviceSyncStateEntity> DeviceSyncStates => Set<DeviceSyncStateEntity>();
    public DbSet<SyncBatchEntity> SyncBatches => Set<SyncBatchEntity>();
    public DbSet<SyncErrorEntity> SyncErrors => Set<SyncErrorEntity>();
    public DbSet<StatusNormalizationRuleEntity> StatusNormalizationRules => Set<StatusNormalizationRuleEntity>();
    public DbSet<SavedReportEntity> SavedReports => Set<SavedReportEntity>();
    public DbSet<ReportDefinitionEntity> ReportDefinitions => Set<ReportDefinitionEntity>();
    public DbSet<ReportExecutionEntity> ReportExecutions => Set<ReportExecutionEntity>();
    public DbSet<ExportJobEntity> ExportJobs => Set<ExportJobEntity>();
    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();
    public DbSet<StationEntity> Stations => Set<StationEntity>();
    public DbSet<UnitCatalogEntity> UnitCatalog => Set<UnitCatalogEntity>();
    public DbSet<ApiClientDepartmentEntity> ApiClientDepartments => Set<ApiClientDepartmentEntity>();
    public DbSet<IncidentCaseNumberEntity> IncidentCaseNumbers => Set<IncidentCaseNumberEntity>();

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

        modelBuilder.Entity<IngestedIncident>().HasIndex(x => new { x.ApiClientId, x.IncidentId }).IsUnique();
        modelBuilder.Entity<IngestedIncident>().HasIndex(x => new { x.ApiClientId, x.UpdatedAtUtc });
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.IncidentId)
            .HasMaxLength(128);
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.Status)
            .HasMaxLength(64);
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.Agency)
            .HasMaxLength(256);
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.Address)
            .HasMaxLength(512);
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.CallType)
            .HasMaxLength(256);
        modelBuilder.Entity<IngestedIncident>()
            .Property(x => x.CanonicalJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<IngestedIncident>()
            .HasOne(x => x.ApiClient)
            .WithMany()
            .HasForeignKey(x => x.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Agency>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<DepartmentEntity>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasIndex(x => x.DepartmentCode).IsUnique();
            entity.Property(x => x.DepartmentCode).HasMaxLength(4);
            entity.Property(x => x.DepartmentName).HasMaxLength(256);
            entity.Property(x => x.MainAddress).HasMaxLength(512);
            entity.Property(x => x.City).HasMaxLength(128);
            entity.Property(x => x.State).HasMaxLength(64);
            entity.Property(x => x.PostalCode).HasMaxLength(32);
        });
        modelBuilder.Entity<StationEntity>(entity =>
        {
            entity.ToTable("Stations");
            entity.HasIndex(x => new { x.DepartmentId, x.StationNumber }).IsUnique();
            entity.Property(x => x.StationNumber).HasMaxLength(20);
            entity.Property(x => x.StationCode).HasMaxLength(64);
            entity.Property(x => x.Address).HasMaxLength(512);
            entity.Property(x => x.City).HasMaxLength(128);
            entity.Property(x => x.State).HasMaxLength(64);
            entity.Property(x => x.PostalCode).HasMaxLength(32);
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UnitCatalogEntity>(entity =>
        {
            entity.ToTable("Units");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.EquipmentName).HasMaxLength(128);
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Station).WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<ApiClientDepartmentEntity>(entity =>
        {
            entity.ToTable("ApiClientDepartments");
            entity.HasIndex(x => new { x.ApiClientId, x.DepartmentId }).IsUnique();
            entity.HasOne(x => x.ApiClient).WithMany().HasForeignKey(x => x.ApiClientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<IncidentCaseNumberEntity>(entity =>
        {
            entity.ToTable("IncidentCaseNumbers");
            entity.HasIndex(x => new { x.IncidentId, x.CaseNumber }).IsUnique();
            entity.Property(x => x.CaseNumber).HasMaxLength(30);
            entity.Property(x => x.AgencyPrefix).HasMaxLength(8);
            entity.Property(x => x.Source).HasMaxLength(30);
            entity.HasOne(x => x.Incident).WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.NormalizedUserName).IsUnique();
        modelBuilder.Entity<RoleDefinition>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<PermissionDefinition>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<UserAgencyMembership>().HasIndex(x => new { x.UserId, x.AgencyId }).IsUnique();
        modelBuilder.Entity<UserRoleAssignment>().HasIndex(x => new { x.UserId, x.RoleId, x.AgencyId }).IsUnique();
        modelBuilder.Entity<RolePermissionAssignment>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        modelBuilder.Entity<UserPermissionOverride>().HasIndex(x => new { x.UserId, x.PermissionId, x.AgencyId }).IsUnique();

        modelBuilder.Entity<IncidentEntity>(entity =>
        {
            entity.HasIndex(x => x.ExternalIncidentId).IsUnique();
            entity.Property(x => x.Type).HasMaxLength(256);
            entity.Property(x => x.Priority).HasMaxLength(64);
            entity.Property(x => x.Address).HasMaxLength(512);
            entity.Property(x => x.LocationName).HasMaxLength(256);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.Municipality).HasMaxLength(64);
            entity.Property(x => x.TypeCode).HasMaxLength(64);
            entity.Property(x => x.Subtype).HasMaxLength(256);
            entity.Property(x => x.SubtypeCode).HasMaxLength(64);
            entity.Property(x => x.Coordinates).HasMaxLength(64);
            entity.Property(x => x.RawPayloadJson).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<IncidentAgencyEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.AgencyId, x.AgencyEventId }).IsUnique();
            entity.Property(x => x.AgencyEventId).HasMaxLength(128);
            entity.Property(x => x.DispatchGroup).HasMaxLength(64);
            entity.Property(x => x.CaseNumber).HasMaxLength(128);
        });
        modelBuilder.Entity<IncidentCallerEntity>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.PhoneNumber).HasMaxLength(128);
            entity.Property(x => x.Address).HasMaxLength(512);
            entity.Property(x => x.City).HasMaxLength(128);
        });
        modelBuilder.Entity<IncidentCommentEntity>(entity =>
        {
            entity.Property(x => x.Message).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedBy).HasMaxLength(128);
            entity.Property(x => x.CreatedAgency).HasMaxLength(128);
        });
        modelBuilder.Entity<IncidentUnitEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.AgencyId, x.UnitIdentifier }).IsUnique();
            entity.Property(x => x.UnitIdentifier).HasMaxLength(128);
            entity.Property(x => x.Station).HasMaxLength(128);
            entity.Property(x => x.UnitType).HasMaxLength(128);
            entity.Property(x => x.AgencyRaw).HasMaxLength(64);
            entity.Property(x => x.StatusOriginal).HasMaxLength(8);
            entity.Property(x => x.CurrentStatus).HasMaxLength(64);
            entity.Property(x => x.Comment).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.UnitCatalog).WithMany().HasForeignKey(x => x.UnitCatalogId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<UnitStatusEventEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.AgencyId, x.UnitIdentifier, x.OccurredAtUtc, x.StatusCodeRaw });
            entity.Property(x => x.UnitIdentifier).HasMaxLength(128);
            entity.Property(x => x.StatusCodeRaw).HasMaxLength(64);
            entity.Property(x => x.StatusCodeNormalized).HasMaxLength(64);
            entity.Property(x => x.SourceText).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedBy).HasMaxLength(128);
            entity.Property(x => x.CreatedAgency).HasMaxLength(128);
        });
        modelBuilder.Entity<UnitTimelineFactEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.AgencyId, x.UnitIdentifier }).IsUnique();
            entity.Property(x => x.UnitIdentifier).HasMaxLength(128);
        });
        modelBuilder.Entity<IncidentAssignmentEntity>(entity =>
        {
            entity.HasIndex(x => new { x.IncidentId, x.AgencyId, x.DeviceId, x.AssignmentKind });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.AssignmentKind).HasMaxLength(64);
        });
        modelBuilder.Entity<IncidentSyncLogEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.ChangedAtUtc, x.AckedAtUtc });
            entity.Property(x => x.Scope).HasMaxLength(64);
            entity.Property(x => x.ChangeType).HasMaxLength(64);
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Notes).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<DeviceEntity>(entity =>
        {
            entity.HasIndex(x => new { x.ApiClientId, x.DeviceId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.DeviceName).HasMaxLength(256);
            entity.Property(x => x.MachineName).HasMaxLength(256);
            entity.Property(x => x.DeviceType).HasMaxLength(128);
        });

        modelBuilder.Entity<DeviceAgencyEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceRefId, x.AgencyId }).IsUnique();

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Agency)
                .WithMany()
                .HasForeignKey(x => x.AgencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceSettingEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.AgencyId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GlobalSettingEntity>(entity =>
        {
            entity.HasIndex(x => x.AgencyId).IsUnique();
            entity.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Agency)
                .WithMany()
                .HasForeignKey(x => x.AgencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceHeartbeatEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.ReceivedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.Message).HasColumnType("nvarchar(max)");
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceSyncStateEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.AgencyId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.LastBatchId).HasMaxLength(64);
            entity.Property(x => x.ConflictPolicy).HasMaxLength(64);
            entity.Property(x => x.StateJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncBatchEntity>(entity =>
        {
            entity.HasIndex(x => x.BatchId).IsUnique();
            entity.HasIndex(x => new { x.DeviceId, x.StartedAtUtc });
            entity.Property(x => x.BatchId).HasMaxLength(64);
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Direction).HasMaxLength(16);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Notes).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncErrorEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.CreatedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Scope).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64);
            entity.Property(x => x.Message).HasColumnType("nvarchar(max)");
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceRefId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<SyncErrorEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.CreatedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Scope).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64);
            entity.Property(x => x.Message).HasColumnType("nvarchar(max)");
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<StatusNormalizationRuleEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.RawCode }).IsUnique();
            entity.Property(x => x.RawCode).HasMaxLength(64);
            entity.Property(x => x.NormalizedCode).HasMaxLength(128);
        });
        modelBuilder.Entity<SavedReportEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.ReportType).HasMaxLength(64);
            entity.Property(x => x.ParametersJson).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<ReportDefinitionEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Description).HasColumnType("nvarchar(max)");
            entity.Property(x => x.DefaultParametersJson).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<ReportExecutionEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.ExecutedAtUtc });
            entity.Property(x => x.ReportType).HasMaxLength(64);
            entity.Property(x => x.ParametersJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResultSummaryJson).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<ExportJobEntity>(entity =>
        {
            entity.HasIndex(x => new { x.AgencyId, x.CreatedAtUtc });
            entity.Property(x => x.Format).HasMaxLength(32);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.PayloadText).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<SourceSystemEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SourceSystemEntity>().HasData(
            new SourceSystemEntity { Id = (short)SourceSystemCode.IAR, Code = "IAR", Description = "IAR JSON feed" },
            new SourceSystemEntity { Id = (short)SourceSystemCode.EmailText, Code = "EmailText", Description = "Email body parsing" }
        );
    }
}
