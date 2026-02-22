using System;
using System.Collections.Generic;

namespace modern_auth_api.Entity;

public partial class UsersToken
{
    public long Id { get; set; }

    public string Token { get; set; } = null!;

    public DateTime CreateTime { get; set; }

    public DateTime ExpireTime { get; set; }

    public long UsersId { get; set; }

    public string Type { get; set; } = null!;

    public virtual User Users { get; set; } = null!;
}
