using System;
using System.Collections.Generic;

namespace CodexEnvironmentManager.Models;

public class Persona
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "\U0001F464";
    public string AgentsTemplatePath { get; set; } = "";
    public string ApprovalsReviewer { get; set; } = "user";
    private KimiProfileOptions _kimiOptions = new();
    public KimiProfileOptions KimiOptions
    {
        get => _kimiOptions;
        set => _kimiOptions = value ?? new KimiProfileOptions();
    }
    public Dictionary<string, string> ConfigOverrides { get; set; } = new();
    public Dictionary<string, string> EnvVars { get; set; } = new();
    public List<string> CliArgs { get; set; } = new();

    public string Model => GetConfigValue("model");
    public bool IsKimiProvider => IsKimiModel(Model);

    // Computed display for UI binding
    public string Display => $"{Icon} {Name}";

    public static bool IsKimiModel(string? model) =>
        !string.IsNullOrWhiteSpace(model) && model.StartsWith("kimi", StringComparison.OrdinalIgnoreCase);

    private string GetConfigValue(string key)
    {
        foreach (var kv in ConfigOverrides)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return string.Empty;
    }
}

public sealed class KimiProfileOptions
{
    public string ThinkingMode { get; set; } = "default";
    public bool PlanMode { get; set; }
    private List<string> _skillsDirs = new();
    public List<string> SkillsDirs
    {
        get => _skillsDirs;
        set => _skillsDirs = value ?? new List<string>();
    }

    public string McpConfigFile { get; set; } = "";
    private List<string> _additionalDirs = new();
    public List<string> AdditionalDirs
    {
        get => _additionalDirs;
        set => _additionalDirs = value ?? new List<string>();
    }
}
