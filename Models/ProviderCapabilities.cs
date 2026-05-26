using System;

namespace CodexEnvironmentManager.Models;

public sealed record ProviderCapability(
    string ProviderId,
    bool SupportsDesktop,
    bool SupportsCli,
    bool SupportsApiKey,
    bool SupportsOauth,
    string ProfileMechanism,
    bool SupportsThinkingMode = false,
    bool SupportsPlanMode = false,
    bool SupportsSkillsDir = false,
    bool SupportsMcpConfig = false,
    bool SupportsAdditionalWorkspaceDirs = false);

public static class ProviderCapabilities
{
    public const string CodexProvider = "codex";
    public const string KimiProvider = "kimi";

    private static readonly ProviderCapability Codex = new(
        ProviderId: CodexProvider,
        SupportsDesktop: true,
        SupportsCli: true,
        SupportsApiKey: true,
        SupportsOauth: true,
        ProfileMechanism: "model_instructions_file");

    private static readonly ProviderCapability Kimi = new(
        ProviderId: KimiProvider,
        SupportsDesktop: false,
        SupportsCli: true,
        SupportsApiKey: true,
        SupportsOauth: true,
        ProfileMechanism: "agent_file",
        SupportsThinkingMode: true,
        SupportsPlanMode: true,
        SupportsSkillsDir: true,
        SupportsMcpConfig: true,
        SupportsAdditionalWorkspaceDirs: true);

    public static ProviderCapability ForProvider(string? provider) =>
        string.Equals(provider?.Trim(), KimiProvider, StringComparison.OrdinalIgnoreCase)
            ? Kimi
            : Codex;

    public static ProviderCapability ForModel(string? model) =>
        Persona.IsKimiModel(model) ? Kimi : Codex;

    public static bool IsKimiProvider(string? provider) =>
        string.Equals(ForProvider(provider).ProviderId, KimiProvider, StringComparison.OrdinalIgnoreCase);

    public static bool SupportsDesktop(string? provider) => ForProvider(provider).SupportsDesktop;
    public static bool SupportsCli(string? provider) => ForProvider(provider).SupportsCli;
    public static bool SupportsApiKey(string? provider) => ForProvider(provider).SupportsApiKey;
    public static bool SupportsOauth(string? provider) => ForProvider(provider).SupportsOauth;
    public static string ProfileMechanism(string? provider) => ForProvider(provider).ProfileMechanism;
}
