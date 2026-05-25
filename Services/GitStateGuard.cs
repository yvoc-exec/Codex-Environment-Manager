using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexEnvironmentManager.Services;

public class GitStateGuard
{
    public void Check(string repoPath)
    {
        if (!IsInsideWorkTree(repoPath)) return;

        var status = RunGit(repoPath, "status", "--porcelain");
        if (status.ExitCode != 0)
        {
            WpfMessageBox.Show($"Git status failed:\n\n{status.Error}", "Git Guard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(status.Output)) return;

        var lines = status.Output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var display = lines.Count > 20
            ? string.Join(Environment.NewLine, lines.Take(20)) + $"{Environment.NewLine}... and {lines.Count - 20} more files"
            : status.Output;

        var result = WpfMessageBox.Show(
            $"Uncommitted changes detected in:\n{repoPath}\n\nCreate a timestamped patch backup before launching?\n\nYes = create patch backup and continue\nNo = continue without backup\nCancel = abort launch\n\n{display}",
            "Git Guard",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            throw new OperationCanceledException("Launch cancelled: dirty git state.");

        if (result == MessageBoxResult.Yes)
            CreatePatchBackup(repoPath, status.Output);
    }

    private static bool IsInsideWorkTree(string repoPath)
    {
        var result = RunGit(repoPath, "rev-parse", "--is-inside-work-tree");
        return result.ExitCode == 0 && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static void CreatePatchBackup(string repoPath, string porcelainStatus)
    {
        var rootResult = RunGit(repoPath, "rev-parse", "--show-toplevel");
        var root = rootResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(rootResult.Output)
            ? rootResult.Output.Trim()
            : repoPath;

        var backupDir = Path.Combine(JunctionManager.SwitcherDir, "git-backups", SanitizePathPart(root));
        Directory.CreateDirectory(backupDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var patchPath = Path.Combine(backupDir, $"dirty_{stamp}.patch");
        var metaPath = Path.Combine(backupDir, $"dirty_{stamp}.txt");

        var diff = RunGit(repoPath, "diff", "--binary");
        var staged = RunGit(repoPath, "diff", "--cached", "--binary");

        var sb = new StringBuilder();
        sb.AppendLine("# Working tree diff");
        sb.AppendLine(diff.Output);
        sb.AppendLine();
        sb.AppendLine("# Staged diff");
        sb.AppendLine(staged.Output);
        File.WriteAllText(patchPath, sb.ToString(), Encoding.UTF8);

        var untracked = ExtractUntrackedPaths(porcelainStatus).ToList();
        string? untrackedZip = null;
        if (untracked.Count > 0)
            untrackedZip = CreateUntrackedZip(root, backupDir, stamp, untracked);

        File.WriteAllText(metaPath,
            "Git Guard backup created before Codex launch." + Environment.NewLine +
            $"Repo: {root}" + Environment.NewLine +
            $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
            $"Patch: {patchPath}" + Environment.NewLine +
            $"Untracked zip: {untrackedZip ?? "(none)"}" + Environment.NewLine +
            Environment.NewLine +
            "Porcelain status:" + Environment.NewLine +
            porcelainStatus,
            Encoding.UTF8);

        var extra = untrackedZip != null
            ? $"\n\nUntracked file contents were archived to:\n{untrackedZip}"
            : "";
        WpfMessageBox.Show(
            $"Patch backup created:\n{patchPath}{extra}",
            "Git Guard Backup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static IEnumerable<string> ExtractUntrackedPaths(string porcelainStatus)
    {
        foreach (var raw in porcelainStatus.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (!line.StartsWith("?? ", StringComparison.Ordinal)) continue;
            var path = line[3..].Trim();
            if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
                path = path[1..^1].Replace("\\\"", "\"");
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }
    }

    private static string? CreateUntrackedZip(string repoRoot, string backupDir, string stamp, IEnumerable<string> relativePaths)
    {
        var zipPath = Path.Combine(backupDir, $"untracked_{stamp}.zip");
        var repoFull = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var added = 0;

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var relative in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(Path.Combine(repoRoot, relative));
            if (!full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(full))
            {
                AddFileToArchive(archive, repoFull, full);
                added++;
            }
            else if (Directory.Exists(full))
            {
                foreach (var file in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    if (file.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                    AddFileToArchive(archive, repoFull, file);
                    added++;
                }
            }
        }

        if (added > 0) return zipPath;
        try { File.Delete(zipPath); } catch { }
        return null;
    }

    private static void AddFileToArchive(ZipArchive archive, string repoFull, string file)
    {
        var entryName = Path.GetFullPath(file)[repoFull.Length..].Replace(Path.DirectorySeparatorChar, '/');
        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
    }

    private static string SanitizePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((value ?? "repo").Select(c => invalid.Contains(c) || c == ':' || c == '\\' || c == '/' ? '_' : c).ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "repo" : cleaned;
    }

    private static (int ExitCode, string Output, string Error) RunGit(string repoPath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoPath);
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return (-1, "", "Failed to start git process.");

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (-1, outputTask.IsCompleted ? outputTask.Result : "", "git command timed out after 30 seconds.");
            }

            Task.WaitAll(outputTask, errorTask);
            return (proc.ExitCode, outputTask.Result, errorTask.Result);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }
}
