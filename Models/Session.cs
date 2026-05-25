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
    public int? ProcessId { get; set; }
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
    public string DisplayName => $"{AccountName} + {PersonaName} + {WorkspaceName}";
}
