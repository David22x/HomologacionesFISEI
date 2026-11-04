using Microsoft.AspNetCore.Identity;

namespace AuthService.Models;

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Descripcion { get; set; }

    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}