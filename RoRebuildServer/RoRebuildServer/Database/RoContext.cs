using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;

namespace RoRebuildServer.Database;

#pragma warning disable CS8618 // Disable null usage warnings, as it thinks dbsets can be null

public class RoContext : IdentityDbContext<RoUserAccount, UserRole, int>
{
    public RoContext(DbContextOptions<RoContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RoUserAccount>().ToTable("DbUserAccount");
        builder.Entity<UserRole>().ToTable("DbRoles");  
        
        builder.Entity<IdentityUserRole<int>>().ToTable("DbUserRoles");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("DbRoleClaims");
        builder.Entity<IdentityUserClaim<int>>().ToTable("DbUserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("DbUserLogins");
        builder.Entity<IdentityUserToken<int>>().ToTable("DbUserTokens");
        
        builder.Entity<RoUserAccount>().HasMany<DbCharacter>(c => c.Characters).WithOne(o => o.Account).HasForeignKey("AccountId");
        builder.Entity<DbCharacter>().HasIndex(c => c.Name).IsUnique();
        builder.Entity<StorageInventory>().HasOne<DbCharacter>(c => c.Character);
    }


    public DbSet<DbCharacter> Character { get; set; }
    public DbSet<StorageInventory> StorageInventory { get; set; }
}