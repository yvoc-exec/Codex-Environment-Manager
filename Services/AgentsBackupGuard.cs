using System;
using System.IO;

namespace CodexEnvironmentManager.Services;

public static class AgentsBackupGuard
{
    public static void BackupIfExists(string workspacePath)
    {
        var agentsPath = Path.Combine(workspacePath, "AGENTS.md");
        if (!File.Exists(agentsPath)) return;

        var backupName = $"AGENTS.md.backup.{DateTime.Now:yyyyMMdd_HHmmss}";
        var backupPath = Path.Combine(workspacePath, backupName);
        File.Copy(agentsPath, backupPath, overwrite: true);
    }
}
