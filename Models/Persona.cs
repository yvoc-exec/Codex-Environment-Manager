using System;
using System.Collections.Generic;

namespace CodexEnvironmentManager.Models;

public class Persona
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "👤";
    public string AgentsTemplatePath { get; set; } = "";
    public string ApprovalsReviewer { get; set; } = "user";
    public Dictionary<string, string> ConfigOverrides { get; set; } = new();
    public Dictionary<string, string> EnvVars { get; set; } = new();
    public List<string> CliArgs { get; set; } = new();

    // Computed display for UI binding
    public string Display => $"{Icon} {Name}";
}
