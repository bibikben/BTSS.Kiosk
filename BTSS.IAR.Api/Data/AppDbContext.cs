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

        modelBuilder.Entity<SourceSystemEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SourceSystemEntity>().HasData(
            new SourceSystemEntity { Id = (short)SourceSystemCode.IAR, Code = "IAR", Description = "IAR JSON feed" },
            new SourceSystemEntity { Id = (short)SourceSystemCode.EmailText, Code = "EmailText", Description = "Email body parsing" }
        );
    }
}
