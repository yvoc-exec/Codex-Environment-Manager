using System;

namespace CodexEnvironmentManager.Services;

public sealed class SessionKillResult
{
    public string SessionId { get; init; } = "";
    public string SessionType { get; init; } = "";
    public bool TargetResolved { get; init; }
    public int? TargetProcessId { get; init; }
    public bool KillAttempted { get; init; }
    public bool KillConfirmed { get; init; }
    public bool SessionRemoved { get; init; }
    public string Message { get; init; } = "";

    public static SessionKillResult NotFound(string sessionId) =>
        new()
        {
            SessionId = sessionId,
            Message = $"Session '{sessionId}' was not found."
        };

    public static SessionKillResult Build(string sessionId, string sessionType, bool targetResolved, int? targetProcessId, bool killAttempted, bool killConfirmed, bool sessionRemoved, string message) =>
        new()
        {
            SessionId = sessionId,
            SessionType = sessionType,
            TargetResolved = targetResolved,
            TargetProcessId = targetProcessId,
            KillAttempted = killAttempted,
            KillConfirmed = killConfirmed,
            SessionRemoved = sessionRemoved,
            Message = message
        };
}
