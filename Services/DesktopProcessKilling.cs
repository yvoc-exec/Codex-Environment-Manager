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
    public bool HasLiveDesktop { get; init; }
    public bool IsAmbiguous { get; init; }
    public DesktopProcessTarget? UniqueTarget { get; init; }
    public string Message { get; init; } = "";

    public static BestEffortDesktopSessionInspection NoDesktop(string message) =>
        new()
        {
            Message = message
        };

    public static BestEffortDesktopSessionInspection Unique(DesktopProcessTarget target, string message) =>
        new()
        {
            HasLiveDesktop = true,
            UniqueTarget = target,
            Message = message
        };

    public static BestEffortDesktopSessionInspection Ambiguous(string message) =>
        new()
        {
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

        if (candidates.Count == 0) return null;

        var bestScore = candidates.Max(x => x.Score);
        var best = candidates.Where(x => x.Score == bestScore).ToList();
        if (best.Count != 1) return null;

        var match = best[0].Process;
        return new DesktopProcessTarget(match.ProcessId, match.Name, match.CommandLine);
    }

    internal static IReadOnlyList<(CodexProcessSnapshot Process, int Score)> GetPlausibleCandidates(IEnumerable<CodexProcessSnapshot> processes)
    {
        if (processes == null) return Array.Empty<(CodexProcessSnapshot Process, int Score)>();

        return processes
            .Where(IsCodexProcess)
            .Select(p => (Process: p, Score: ScoreCandidate(p.CommandLine)))
            .Where(x => x.Score > 0)
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
            return 2;

        var normalized = commandLine.ToLowerInvariant();
        if (normalized.Contains("--cd") || normalized.Contains("--profile"))
            return 0;

        if (normalized.Contains(" app "))
            return 1;

        return 2;
    }
}
