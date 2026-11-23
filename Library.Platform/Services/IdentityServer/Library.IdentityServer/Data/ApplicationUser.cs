using Microsoft.AspNetCore.Identity;

namespace Library.IdentityServer.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
