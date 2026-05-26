using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public class AccountManager
{
    public sealed class AccountDeleteResult
    {
        public bool RemovedFromConfig { get; init; }
        public bool Quarantined { get; init; }
        public bool DeletedFromDisk { get; init; }
        public string? QuarantinePath { get; init; }
        public string? LockedPath { get; init; }
        public string Message { get; init; } = "";
    }

    private readonly ConfigService _config;
    private readonly LogService? _log;

    public AccountManager(ConfigService config, LogService? log = null)
    {
        _config = config;
        _log = log;
    }

    public List<Account> GetAccounts()
    {
        var list = _config.LoadList<Account>("accounts");
        foreach (var acct in list)
        {
            if (string.IsNullOrWhiteSpace(acct.Provider))
            {
                acct.Provider = "codex";
            }
        }
        return list;
    }

    public void AddPlusAccount(string name)
    {
        var list = GetAccounts();
        EnsureUniqueName(list, name);
        var acct = new Account { Name = name, Type = "plus", Provider = "codex" };
        JunctionManager.CreateAccountProfile(acct.Id);
        PersonaEngine.EnsureAccountBaseConfig(acct.Id);
        list.Add(acct);
        _config.SaveList("accounts", list);
    }

    public void AddApiKeyAccount(string name, string apiKey)
    {
        var list = GetAccounts();
        EnsureUniqueName(list, name);
        var acct = new Account
        {
            Name = name,
            Type = "api_key",
            Provider = "codex",
            ApiKeyEncrypted = DpapiHelper.EncryptToBase64(apiKey)
        };
        JunctionManager.CreateAccountProfile(acct.Id);
        PersonaEngine.EnsureAccountBaseConfig(acct.Id);
        list.Add(acct);
        _config.SaveList("accounts", list);

        // Best effort bootstrap. This avoids passing API keys to every future Codex launch.
        // Run in the background so account creation never freezes the WPF UI.
        _ = Task.Run(() => TryBootstrapApiKeyLoginAsync(acct, apiKey));
    }

    public void AddKimiOAuthAccount(string name)
    {
        var list = GetAccounts();
        EnsureUniqueName(list, name);
        var acct = new Account
        {
            Name = name,
            Type = "kimi_oauth",
            Provider = "kimi"
        };
        list.Add(acct);
        _config.SaveList("accounts", list);

        var kimiHome = JunctionManager.GetKimiAccountHomePath(acct.Id);
        Directory.CreateDirectory(kimiHome);
    }

    public void AddKimiApiKeyAccount(string name, string apiKey)
    {
        var list = GetAccounts();
        EnsureUniqueName(list, name);
        var acct = new Account
        {
            Name = name,
            Type = "moonshot_api_key",
            Provider = "kimi",
            ApiKeyEncrypted = DpapiHelper.EncryptToBase64(apiKey)
        };
        list.Add(acct);
        _config.SaveList("accounts", list);

        var kimiHome = JunctionManager.GetKimiAccountHomePath(acct.Id);
        Directory.CreateDirectory(kimiHome);
    }

    public bool CanDelete(string accountId)
    {
        var active = JunctionManager.LoadActiveAccount();
        return !string.Equals(active, accountId, StringComparison.OrdinalIgnoreCase);
    }

    public AccountDeleteResult DeleteAccount(string id)
    {
        if (!CanDelete(id))
            throw new InvalidOperationException("Cannot delete the currently active account. Switch to another account first.");

        var account = GetAccounts().FirstOrDefault(a => a.Id == id);
        var isKimi = account != null && string.Equals(account.ResolvedProvider, "kimi", StringComparison.OrdinalIgnoreCase);

        var path = JunctionManager.GetAccountProfilePath(id);
        var quarantineRoot = Path.Combine(JunctionManager.SwitcherDir, "deleted_accounts");
        var quarantinePath = "";
        var kimiQuarantinePath = "";

        if (Directory.Exists(path))
        {
            Directory.CreateDirectory(quarantineRoot);
            quarantinePath = Path.Combine(quarantineRoot, $"{id}_{DateTime.Now:yyyyMMdd_HHmmss}");

            ClearReadOnlyAttributes(path);

            try
            {
                Directory.Move(path, quarantinePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log?.Warn($"Account folder quarantine failed for {id}: {ex.Message}");
                throw new IOException(
                    $"Could not quarantine the account folder. A file may be locked or access denied.\n\nPath: {path}\n\nDetails: {ex.Message}",
                    ex);
            }
        }

        if (isKimi)
        {
            var kimiPath = JunctionManager.GetKimiAccountHomePath(id);
            if (Directory.Exists(kimiPath))
            {
                Directory.CreateDirectory(quarantineRoot);
                kimiQuarantinePath = Path.Combine(quarantineRoot, $"kimi_{id}_{DateTime.Now:yyyyMMdd_HHmmss}");
                ClearReadOnlyAttributes(kimiPath);
                try
                {
                    Directory.Move(kimiPath, kimiQuarantinePath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _log?.Warn($"Kimi account home quarantine failed for {id}: {ex.Message}");
                    // Do not fail the whole deletion; continue and report cleanup needed.
                }
            }
        }

        var list = GetAccounts();
        list.RemoveAll(a => a.Id == id);
        _config.SaveList("accounts", list);

        var deleted = TryDeleteDirectoryWithRetries(quarantinePath, out var lockedPath, out var error);
        var kimiDeleted = string.IsNullOrWhiteSpace(kimiQuarantinePath) || TryDeleteDirectoryWithRetries(kimiQuarantinePath, out _, out _);

        if (deleted && (string.IsNullOrWhiteSpace(kimiQuarantinePath) || kimiDeleted))
        {
            return new AccountDeleteResult
            {
                RemovedFromConfig = true,
                Quarantined = !string.IsNullOrWhiteSpace(quarantinePath),
                DeletedFromDisk = true,
                QuarantinePath = quarantinePath,
                Message = "Account removed and account folder deleted."
            };
        }

        return new AccountDeleteResult
        {
            RemovedFromConfig = true,
            Quarantined = !string.IsNullOrWhiteSpace(quarantinePath) || !string.IsNullOrWhiteSpace(kimiQuarantinePath),
            DeletedFromDisk = false,
            QuarantinePath = quarantinePath,
            LockedPath = lockedPath,
            Message =
                "Account was removed from CEM and its folder was moved to quarantine, but Windows would not allow full deletion yet.\n\n" +
                $"Quarantine folder:\n{quarantinePath}\n\n" +
                (string.IsNullOrWhiteSpace(lockedPath) ? "" : $"Likely locked file:\n{lockedPath}\n\n") +
                $"Details:\n{error}\n\n" +
                "Close Codex/Windows Terminal processes if any remain, then delete the quarantine folder later."
        };
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Prepend(root))
            {
                try
                {
                    var attrs = File.GetAttributes(path);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
                }
                catch { /* best effort */ }
            }
        }
        catch { /* best effort */ }
    }

    private static bool TryDeleteDirectoryWithRetries(string path, out string? lockedPath, out string? error)
    {
        lockedPath = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return true;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                ClearReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                lockedPath = TryExtractQuotedPath(ex.Message) ?? FindLikelyLockedFile(path);
                Thread.Sleep(250 * attempt);
            }
        }

        return false;
    }

    private static string? TryExtractQuotedPath(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var first = message.IndexOf('\'');
        var last = message.LastIndexOf('\'');
        if (first >= 0 && last > first)
            return message[(first + 1)..last];
        return null;
    }

    private static string? FindLikelyLockedFile(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "pack-*.idx", SearchOption.AllDirectories).FirstOrDefault()
                   ?? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public string? DecryptApiKey(Account acct) =>
        !string.IsNullOrWhiteSpace(acct.ApiKeyEncrypted)
            ? DpapiHelper.DecryptFromBase64(acct.ApiKeyEncrypted)
            : null;

    public async Task<bool> TryBootstrapApiKeyLoginAsync(Account acct, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;
        if (!string.Equals(acct.ResolvedProvider, "codex", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(acct.Type, "api_key", StringComparison.OrdinalIgnoreCase)) return false;

        if (!CodexProcessManager.TryFindCodexCliExecutable(out var codexPath) || string.IsNullOrWhiteSpace(codexPath))
        {
            _log?.Warn("Codex CLI not found; API-key account was saved but login bootstrap was skipped.");
            return false;
        }

        var accountPath = JunctionManager.GetAccountProfilePath(acct.Id);
        Directory.CreateDirectory(accountPath);
        PersonaEngine.EnsureAccountBaseConfig(acct.Id);

        try
        {
            var psi = CodexProcessManager.CreateCodexCliProcessStartInfo(codexPath, accountPath, "login", "--with-api-key");
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _log?.Warn("Codex login bootstrap failed: process did not start.");
                return false;
            }

            await proc.StandardInput.WriteLineAsync(apiKey);
            proc.StandardInput.Close();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                _log?.Warn("Codex API-key login bootstrap timed out after 30 seconds.");
                return false;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                _log?.Warn($"Codex API-key login bootstrap failed with exit code {proc.ExitCode}: {stderr}");
                return false;
            }

            _log?.Info("Codex API-key login bootstrap completed for managed account.");
            await TryLogLoginStatusAsync(codexPath, accountPath);
            return true;
        }
        catch (Exception ex)
        {
            _log?.Warn($"Codex API-key login bootstrap failed: {ex.Message}");
            return false;
        }
    }

    private async Task TryLogLoginStatusAsync(string codexPath, string accountPath)
    {
        try
        {
            var psi = CodexProcessManager.CreateCodexCliProcessStartInfo(codexPath, accountPath, "login", "status");
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            using var proc = Process.Start(psi);
            if (proc == null) return;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                _log?.Warn("Codex login status check timed out.");
                return;
            }

            var output = await stdoutTask;
            var err = await stderrTask;
            _log?.Info(proc.ExitCode == 0 ? $"Codex login status: {output.Trim()}" : $"Codex login status failed: {err.Trim()}");
        }
        catch (Exception ex)
        {
            _log?.Warn($"Codex login status check failed: {ex.Message}");
        }
    }

    private static void EnsureUniqueName(IEnumerable<Account> accounts, string name)
    {
        if (accounts.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An account named '{name}' already exists.");
    }
}
