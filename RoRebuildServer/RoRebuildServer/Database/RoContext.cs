using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;

namespace RoRebuildServer.Database;

#pragma warning disable CS8618 // Disable null usage warnings, as it thinks dbsets can be null

public class RoContext : DbContext
{
    public RoContext(DbContextOptions<RoContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }


    public DbSet<DbCharacter> Character { get; set; }
}