using System;
using System.Text.Json.Serialization;

namespace CodexEnvironmentManager.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AccountId { get; set; } = "";
    public string PersonaId { get; set; } = "";
    public string WorkspaceId { get; set; } = "";
    public string Type { get; set; } = "desktop";
    public string AccountProvider { get; set; } = "codex";
    public int? ProcessId { get; set; }
    public string RequestedProfileName { get; set; } = "";
    public string RequestedCodexProfileName { get; set; } = "";
    public string ProfileLaunchMethod { get; set; } = "";
    public string ProfileVerificationStatus { get; set; } = "";
    public string ProfileLaunchCommandPreview { get; set; } = "";
    public string StartedMarkerPath { get; set; } = "";
    public string ExitMarkerPath { get; set; } = "";
    public string StopMarkerPath { get; set; } = "";
    public string LauncherScriptPath { get; set; } = "";
    public string KillMarker { get; set; } = "";
    public string TerminalWindowName { get; set; } = "";
    public string CodexPidPath { get; set; } = "";
    public bool IsBestEffortUntracked { get; set; } = false;
    public DateTime StartTime { get; set; } = DateTime.Now;

    // Enriched display fields (not serialized)
    [JsonIgnore]
    public string AccountName { get; set; } = "";

    [JsonIgnore]
    public string PersonaName { get; set; } = "";

    [JsonIgnore]
    public string WorkspaceName { get; set; } = "";

    [JsonIgnore]
    public string DisplayName =>
        string.Equals(Type, "kimi-cli", StringComparison.OrdinalIgnoreCase)
            ? $"Kimi + {WorkspaceName}"
            : $"{AccountName} + {PersonaName} + {WorkspaceName}";
}
