using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed class KimiPersonaMigrationResult
{
    public bool Changed { get; set; }
    public List<string> MigratedArgs { get; init; } = new();
    public List<string> UnsupportedArgs { get; init; } = new();
    public List<string> ForbiddenArgs { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public bool HasBlockingIssues => UnsupportedArgs.Count > 0 || ForbiddenArgs.Count > 0;

    public string BuildBlockingMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("This Kimi profile contains unsupported raw CLI args.");

        if (ForbiddenArgs.Count > 0)
        {
            sb.AppendLine("Forbidden args controlled by CEM:");
            foreach (var arg in ForbiddenArgs.Distinct(StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- {arg}");
        }

        if (UnsupportedArgs.Count > 0)
        {
            sb.AppendLine("Unsupported legacy args:");
            foreach (var arg in UnsupportedArgs.Distinct(StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- {arg}");
        }

        sb.AppendLine("Move supported settings into the dedicated Kimi options. Remove unsupported raw args before saving or launching.");
        return sb.ToString().TrimEnd();
    }
}

public static class KimiPersonaMigration
{
    private static readonly HashSet<string> SafeBooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--thinking",
        "--no-thinking",
        "--plan"
    };

    private static readonly HashSet<string> SafeValueFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--skills-dir",
        "--mcp-config-file",
        "--add-dir"
    };

    private static readonly HashSet<string> ForbiddenFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--model",
        "--work-dir",
        "--agent-file",
        "--agent"
    };

    public static KimiPersonaMigrationResult Normalize(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        if (!persona.IsKimiProvider)
            return new KimiPersonaMigrationResult();

        persona.KimiOptions ??= new KimiProfileOptions();
        var result = new KimiPersonaMigrationResult();
        var remaining = new List<string>();
        var rawArgs = persona.CliArgs?.ToList() ?? new List<string>();

        for (var i = 0; i < rawArgs.Count; i++)
        {
            var token = rawArgs[i];
            if (string.IsNullOrWhiteSpace(token))
                continue;

            token = token.Trim();
            var normalizedFlag = GetFlagName(token);
            if (string.IsNullOrWhiteSpace(normalizedFlag))
            {
                remaining.Add(token);
                continue;
            }

            if (IsAcpFlag(normalizedFlag))
            {
                result.Warnings.Add("Legacy --acp is no longer supported in CEM Kimi profiles; use Kimi's `kimi acp` flow outside this launcher.");
                result.Changed = true;
                if (HasAttachedValue(token) || TryConsumeValue(rawArgs, ref i, out _)) { }
                continue;
            }

            if (ForbiddenFlags.Contains(normalizedFlag))
            {
                var display = FormatArg(token, TryPeekValue(rawArgs, i, out var peek) ? peek : null);
                result.ForbiddenArgs.Add(display);
                if (!HasAttachedValue(token))
                    TryConsumeValue(rawArgs, ref i, out _);
                continue;
            }

            if (SafeBooleanFlags.Contains(normalizedFlag))
            {
                ApplyBooleanFlag(persona.KimiOptions, normalizedFlag);
                result.MigratedArgs.Add(normalizedFlag);
                result.Changed = true;
                continue;
            }

            if (SafeValueFlags.Contains(normalizedFlag))
            {
                if (!TryReadValue(rawArgs, ref i, out var value))
                {
                    result.UnsupportedArgs.Add(token);
                    continue;
                }

                ApplyValueFlag(persona.KimiOptions, normalizedFlag, value);
                result.MigratedArgs.Add($"{normalizedFlag} {value}");
                result.Changed = true;
                continue;
            }

            result.UnsupportedArgs.Add(token);
            if (!HasAttachedValue(token))
                TryConsumeValue(rawArgs, ref i, out _);
        }

        if (result.Changed || result.HasBlockingIssues)
            persona.CliArgs = remaining;

        return result;
    }

    private static void ApplyBooleanFlag(KimiProfileOptions options, string flag)
    {
        switch (flag)
        {
            case "--thinking":
                if (IsDefaultThinkingMode(options.ThinkingMode))
                    options.ThinkingMode = "thinking";
                break;
            case "--no-thinking":
                if (IsDefaultThinkingMode(options.ThinkingMode))
                    options.ThinkingMode = "no-thinking";
                break;
            case "--plan":
                options.PlanMode = true;
                break;
        }
    }

    private static void ApplyValueFlag(KimiProfileOptions options, string flag, string value)
    {
        switch (flag)
        {
            case "--skills-dir":
                if (!options.SkillsDirs.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                    options.SkillsDirs.Add(value);
                break;
            case "--mcp-config-file":
                if (string.IsNullOrWhiteSpace(options.McpConfigFile))
                    options.McpConfigFile = value;
                break;
            case "--add-dir":
                if (!options.AdditionalDirs.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                    options.AdditionalDirs.Add(value);
                break;
        }
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        var token = args[index];
        if (HasAttachedValue(token))
        {
            value = GetAttachedValue(token);
            return !string.IsNullOrWhiteSpace(value);
        }

        if (TryConsumeValue(args, ref index, out value))
            return true;

        value = string.Empty;
        return false;
    }

    private static bool TryConsumeValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Count)
            return false;

        var next = args[index + 1];
        if (string.IsNullOrWhiteSpace(next) || LooksLikeFlag(next))
            return false;

        index++;
        value = next.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryPeekValue(IReadOnlyList<string> args, int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Count)
            return false;

        var next = args[index + 1];
        if (string.IsNullOrWhiteSpace(next) || LooksLikeFlag(next))
            return false;

        value = next.Trim();
        return true;
    }

    private static bool LooksLikeFlag(string token) =>
        token.StartsWith("-", StringComparison.Ordinal) && !IsWindowsPath(token);

    private static bool IsWindowsPath(string token) =>
        token.Length >= 2 && char.IsLetter(token[0]) && token[1] == ':';

    private static string GetFlagName(string token)
    {
        var trimmed = token.Trim();
        if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            return string.Empty;

        var eqIndex = trimmed.IndexOf('=');
        return eqIndex > 0 ? trimmed[..eqIndex] : trimmed;
    }

    private static bool HasAttachedValue(string token) =>
        token.IndexOf('=') > 0;

    private static string GetAttachedValue(string token) =>
        HasAttachedValue(token) ? token[(token.IndexOf('=') + 1)..].Trim() : string.Empty;

    private static bool IsAcpFlag(string normalizedFlag) =>
        string.Equals(normalizedFlag, "--acp", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultThinkingMode(string? mode) =>
        string.IsNullOrWhiteSpace(mode) ||
        string.Equals(mode.Trim(), "default", StringComparison.OrdinalIgnoreCase);

    private static string FormatArg(string flagToken, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return flagToken;
        return $"{GetFlagName(flagToken)} {value.Trim()}";
    }
}
