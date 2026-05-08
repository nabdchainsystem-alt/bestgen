using Microsoft.AspNetCore.Identity;

namespace bestgen.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string PreferredLanguage { get; set; } = "ar-SA";
}
