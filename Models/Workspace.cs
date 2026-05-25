using System;

namespace CodexEnvironmentManager.Models;

public class Workspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? ProjectTemplate { get; set; }
    public string? LastAccountId { get; set; }
    public string? LastPersonaId { get; set; }
    public string? LastLaunchType { get; set; } // "desktop" or "cli"
}
