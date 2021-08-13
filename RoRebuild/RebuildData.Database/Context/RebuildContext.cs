using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RebuildData.Database.Domain;

namespace RebuildData.Database.Context
{
    class RebuildContext : IdentityDbContext<RebuildUser, ApplicationRole, int>
    {
        public RebuildContext(DbContextOptions<RebuildContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RebuildUser>().ToTable("DbUsers");
            builder.Entity<ApplicationRole>().ToTable("DbRoles");

            builder.Entity<IdentityUserRole<int>>().ToTable("DbUserRoles");
            builder.Entity<IdentityRoleClaim<int>>().ToTable("DbRoleClaims");
            builder.Entity<IdentityUserClaim<int>>().ToTable("DbUserClaims");
            builder.Entity<IdentityUserLogin<int>>().ToTable("DbUserLogins");
            builder.Entity<IdentityUserToken<int>>().ToTable("DbUserTokens");
        }

    }

    public class ApplicationRole : IdentityRole<int>
    {
        public ApplicationRole(string name) : base(name)
        {
        }


    }
}
