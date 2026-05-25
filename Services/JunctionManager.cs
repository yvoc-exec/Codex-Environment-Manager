using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace CodexEnvironmentManager.Services;

public static class JunctionManager
{
    private static readonly Mutex JunctionMutex = new(false, @"Local\CodexEnvironmentManager_Junction");

    public static string CodexHome => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    public static string SwitcherDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex-switcher");

    public static void EnsureSwitcherDir() => Directory.CreateDirectory(SwitcherDir);

    public static string GetAccountProfilePath(string accountId) =>
        Path.Combine(SwitcherDir, "accounts", accountId);

    public static void CreateAccountProfile(string accountId)
    {
        var path = GetAccountProfilePath(accountId);
        Directory.CreateDirectory(path);
        var configPath = Path.Combine(path, "config.toml");
        if (!File.Exists(configPath))
            File.WriteAllText(configPath, "# Codex config" + Environment.NewLine);
    }

    public static void SaveActiveAccount(string accountId)
    {
        Directory.CreateDirectory(SwitcherDir);
        var path = Path.Combine(SwitcherDir, "active_account.json");
        AtomicWriteText(path, JsonSerializer.Serialize(accountId));
    }

    public static string? LoadActiveAccount()
    {
        var path = Path.Combine(SwitcherDir, "active_account.json");
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<string>(File.ReadAllText(path));
        }
        catch
        {
            var content = File.ReadAllText(path).Trim().Trim('"');
            return string.IsNullOrEmpty(content) ? null : content;
        }
    }

    public static void SwapToAccount(string accountId, LogService? log = null)
    {
        var lockTaken = false;
        try
        {
            lockTaken = JunctionMutex.WaitOne(TimeSpan.FromSeconds(20));
            if (!lockTaken) throw new TimeoutException("Timed out waiting for Codex account switch lock.");
            SwapToAccountCore(accountId, log);
        }
        finally
        {
            if (lockTaken) JunctionMutex.ReleaseMutex();
        }
    }

    private static void SwapToAccountCore(string accountId, LogService? log)
    {
        var target = GetAccountProfilePath(accountId);
        var junction = CodexHome;
        Directory.CreateDirectory(target);

        string? backupPath = null;
        bool createdBackup = false;

        if (Directory.Exists(junction))
        {
            var attr = File.GetAttributes(junction);
            if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                log?.Info("Removing existing Codex junction");
                Directory.Delete(junction, false);
            }
            else
            {
                backupPath = junction + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                log?.Warn($"Existing unmanaged .codex folder detected. Preserving it at {backupPath}");
                Directory.Move(junction, backupPath);
                createdBackup = true;
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $@"/c mklink /J ""{junction}"" ""{target}""",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new IOException($"Failed to create junction: {err} {stdout}");

            if (!Directory.Exists(junction))
                throw new IOException("Junction creation reported success but directory does not exist.");
            var junctionAttr = File.GetAttributes(junction);
            if ((junctionAttr & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                throw new IOException("Created path is not a junction (reparse point missing).");

            log?.Info($"Junction verified: {junction} -> {target}");
            SaveActiveAccount(accountId);

            if (createdBackup && backupPath != null)
                log?.Warn($"Original unmanaged .codex backup was preserved at: {backupPath}. Delete it manually only after verifying the managed profile works.");
        }
        catch
        {
            if (createdBackup && backupPath != null && Directory.Exists(backupPath))
            {
                log?.Warn("Junction creation failed — rolling back to preserved .codex backup");
                try
                {
                    if (Directory.Exists(junction)) Directory.Delete(junction, false);
                    Directory.Move(backupPath, junction);
                    log?.Info("Rollback successful");
                }
                catch (Exception rbEx)
                {
                    log?.Error("Rollback failed", rbEx);
                    throw new InvalidOperationException($"CRITICAL: Junction swap failed AND rollback failed. Manual fix required. Backup at: {backupPath}", rbEx);
                }
            }
            throw;
        }
    }

    private static void AtomicWriteText(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
