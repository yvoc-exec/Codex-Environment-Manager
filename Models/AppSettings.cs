namespace CodexEnvironmentManager.Models;

public class AppSettings
{
    public string CodexDesktopPath { get; set; } = "";
    public string KimiCliPath { get; set; } = "";
    public bool OnboardingCompleted { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool GitGuardEnabled { get; set; } = true;
    public bool PreferWindowsTerminalForCli { get; set; } = false;
    public string WindowsSandboxMode { get; set; } = "elevated";
    public bool TrustWorkspaceOnLaunch { get; set; } = true;

    // Legacy setting retained for backward-compatible settings.json parsing.
    // Active CEM roles now live in CODEX_HOME/AGENTS.md and are selected by Codex profile.
    public bool WriteAgentsMd { get; set; } = false;
}
