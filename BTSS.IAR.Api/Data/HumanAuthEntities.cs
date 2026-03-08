using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace BTSS.IAR.Api.Data;

public sealed class Agency
{
    public int Id { get; set; }
    [MaxLength(128)] public string Code { get; set; } = string.Empty;
    [MaxLength(256)] public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class UserAccount
{
    public int Id { get; set; }
    [MaxLength(128)] public string UserName { get; set; } = string.Empty;
    [MaxLength(256)] public string NormalizedUserName { get; set; } = string.Empty;
    [MaxLength(256)] public string Email { get; set; } = string.Empty;
    [MaxLength(256)] public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsSuperUser { get; set; }
    public int? ActiveAgencyId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public PasswordVerificationResult LastPasswordVerificationResult { get; set; } = PasswordVerificationResult.Failed;
}

public sealed class RoleDefinition
{
    public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = string.Empty;
    [MaxLength(256)] public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
}

public sealed class PermissionDefinition
{
    public int Id { get; set; }
    [MaxLength(128)] public string Code { get; set; } = string.Empty;
    [MaxLength(256)] public string Description { get; set; } = string.Empty;
}

public sealed class UserAgencyMembership
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int AgencyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class UserRoleAssignment
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int? AgencyId { get; set; }
}

public sealed class RolePermissionAssignment
{
    public long Id { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
}

public sealed class UserPermissionOverride
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int PermissionId { get; set; }
    public int? AgencyId { get; set; }
    public bool IsGranted { get; set; } = true;
}
