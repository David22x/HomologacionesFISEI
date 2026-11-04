using AuthService.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public class AuthDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Renombrar tablas de Identity para que queden limpias en AuthDB
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Usuarios");
            b.Property(u => u.NombreCompleto).HasMaxLength(150).IsRequired();
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles");
            b.Property(r => r.Descripcion).HasMaxLength(250);
        });
    }
}