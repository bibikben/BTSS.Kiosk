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
            entity.Property(x => x.CurrentStatus).HasMaxLength(64);
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
        });
        modelBuilder.Entity<DeviceSettingEntity>(entity =>
        {
            entity.HasIndex(x => new { x.DeviceId, x.AgencyId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        });
        modelBuilder.Entity<GlobalSettingEntity>(entity =>
        {
            entity.HasIndex(x => x.AgencyId).IsUnique();
            entity.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<SourceSystemEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SourceSystemEntity>().HasData(
            new SourceSystemEntity { Id = (short)SourceSystemCode.IAR, Code = "IAR", Description = "IAR JSON feed" },
            new SourceSystemEntity { Id = (short)SourceSystemCode.EmailText, Code = "EmailText", Description = "Email body parsing" }
        );
    }
}
