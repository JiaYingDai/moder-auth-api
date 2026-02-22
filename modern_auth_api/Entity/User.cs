using System;
using System.Collections.Generic;

namespace modern_auth_api.Entity;

/// <summary>
/// Registration users on Moneyball Website
/// </summary>
public partial class User
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public DateTime CreateTime { get; set; }

    public DateTime? UpdateTime { get; set; }

    public string ProviderKey { get; set; } = null!;

    public string? Picture { get; set; }

    public string Role { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public bool IsEmailVerified { get; set; }

    public bool Active { get; set; }

    /// <summary>
    /// supabase user id
    /// </summary>
    public string AuthId { get; set; } = null!;

    public virtual ICollection<UsersToken> UsersTokens { get; set; } = new List<UsersToken>();
}
