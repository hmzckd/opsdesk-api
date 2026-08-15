namespace OpsDesk.Infrastructure.Seed;

public sealed class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public bool Enabled { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}