using Microsoft.AspNetCore.Identity;

namespace RoRebuildServer.Database.Domain;

public class RoUserAccount : IdentityUser<int>
{
    public virtual ICollection<DbCharacter>? Characters { get; set; }
    public virtual StorageInventory CharacterStorage { get; set; }

    public byte[]? LoginToken { get; set; }
}

public class UserRole(string name) : IdentityRole<int>(name);