using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed record CodexProcessSnapshot(int ProcessId, string Name, string? CommandLine);

public sealed record DesktopProcessTarget(int ProcessId, string Name, string? CommandLine);

public sealed class BestEffortDesktopSessionInspection
{
    public SessionLiveState State { get; init; } = SessionLiveState.Ambiguous;
    public bool HasLiveDesktop { get; init; }
    public bool IsAmbiguous { get; init; }
    public DesktopProcessTarget? UniqueTarget { get; init; }
    public int? TargetProcessId => UniqueTarget?.ProcessId;
    public string Message { get; init; } = "";

    public static BestEffortDesktopSessionInspection NoDesktop(string message) =>
        new()
        {
            State = SessionLiveState.ClearlyDead,
            Message = message
        };

    public static BestEffortDesktopSessionInspection Unique(DesktopProcessTarget target, string message) =>
        new()
        {
            State = SessionLiveState.Live,
            HasLiveDesktop = true,
            UniqueTarget = target,
            Message = message
        };

    public static BestEffortDesktopSessionInspection Ambiguous(string message) =>
        new()
        {
            State = SessionLiveState.Ambiguous,
            HasLiveDesktop = true,
            IsAmbiguous = true,
            Message = message
        };
}

public sealed class DesktopKillAttemptResult
{
    public bool TargetResolved { get; init; }
    public bool KillAttempted { get; init; }
    public bool KillConfirmed { get; init; }
    public int? TargetProcessId { get; init; }
    public string Message { get; init; } = "";

    public static DesktopKillAttemptResult Inconclusive(string message) =>
        new()
        {
            TargetResolved = false,
            KillAttempted = false,
            KillConfirmed = false,
            TargetProcessId = null,
            Message = message
        };

    public static DesktopKillAttemptResult Confirmed(int processId, string message) =>
        new()
        {
            TargetResolved = true,
            KillAttempted = true,
            KillConfirmed = true,
            TargetProcessId = processId,
            Message = message
        };

    public static DesktopKillAttemptResult Failed(int? processId, string message) =>
        new()
        {
            TargetResolved = processId.HasValue,
            KillAttempted = processId.HasValue,
            KillConfirmed = false,
            TargetProcessId = processId,
            Message = message
        };
}

public interface IBestEffortDesktopTerminator
{
    BestEffortDesktopSessionInspection InspectBestEffortDesktopSession(Session session);
    DesktopKillAttemptResult TryKillBestEffortDesktopSession(Session session);
}

public static class DesktopProcessTargetResolver
{
    public static DesktopProcessTarget? Resolve(IEnumerable<CodexProcessSnapshot> processes)
    {
        var candidates = GetPlausibleCandidates(processes);

        if (candidates.Count != 1) return null;

        var match = candidates[0].Process;
        return new DesktopProcessTarget(match.ProcessId, match.Name, match.CommandLine);
    }

    internal static IReadOnlyList<(CodexProcessSnapshot Process, int Score)> GetPlausibleCandidates(IEnumerable<CodexProcessSnapshot> processes)
    {
        if (processes == null) return Array.Empty<(CodexProcessSnapshot Process, int Score)>();

        return processes
            .Where(IsCodexProcess)
            .Select(p => (Process: p, Score: ScoreCandidate(p.CommandLine)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Process.ProcessId)
            .ToList();
    }

    private static bool IsCodexProcess(CodexProcessSnapshot process) =>
        string.Equals(NormalizeProcessName(process.Name), "Codex", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var trimmed = name.Trim();
        var withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
        return string.IsNullOrWhiteSpace(withoutExtension) ? trimmed : withoutExtension;
    }

    private static int ScoreCandidate(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return 0;

        var normalized = commandLine.ToLowerInvariant();
        if (IsExcludedDesktopCommandLine(normalized))
            return 0;

        var score = 1;
        if (normalized.Contains("codex://"))
            score += 2;

        if (normalized.Contains(@"\app\codex.exe") || normalized.Contains("/app/codex.exe"))
            score += 1;

        return score;
    }

    private static bool IsExcludedDesktopCommandLine(string normalizedCommandLine)
    {
        return normalizedCommandLine.Contains("--type=") ||
               normalizedCommandLine.Contains("app-server") ||
               normalizedCommandLine.Contains("resources\\codex.exe") ||
               normalizedCommandLine.Contains("resources/codex.exe") ||
               normalizedCommandLine.Contains("--cd") ||
               normalizedCommandLine.Contains("--profile");
    }
}
