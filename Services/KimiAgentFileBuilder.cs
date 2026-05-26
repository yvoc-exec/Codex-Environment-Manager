using System;
using System.IO;
using System.Text;

namespace CodexEnvironmentManager.Services;

public sealed record KimiAgentFiles(string SessionDirectory, string AgentFilePath, string PromptFilePath);

public static class KimiAgentFileBuilder
{
    public static KimiAgentFiles CreateSessionFiles(
        string generatedBaseDir,
        string sessionId,
        string? personaName,
        string workspaceName,
        string workspacePath,
        string? roleTemplatePath = null)
    {
        if (string.IsNullOrWhiteSpace(generatedBaseDir))
            throw new ArgumentException("Generated base directory is required.", nameof(generatedBaseDir));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id is required.", nameof(sessionId));

        var sessionDirectory = Path.Combine(generatedBaseDir, "kimi", "sessions", sessionId);
        Directory.CreateDirectory(sessionDirectory);

        var promptPath = Path.Combine(sessionDirectory, "kimi-system.md");
        var agentFilePath = Path.Combine(sessionDirectory, "kimi-agent.yaml");

        File.WriteAllText(promptPath, BuildPromptMarkdown(personaName, workspaceName, workspacePath, roleTemplatePath), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(agentFilePath, BuildAgentYaml(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new KimiAgentFiles(sessionDirectory, agentFilePath, promptPath);
    }

    public static string BuildPromptMarkdown(string? personaName, string workspaceName, string workspacePath, string? roleTemplatePath = null)
    {
        var profileLabel = string.IsNullOrWhiteSpace(personaName) ? "Default Kimi session" : personaName.Trim();
        var safeWorkspaceName = string.IsNullOrWhiteSpace(workspaceName) ? "(unnamed workspace)" : workspaceName.Trim();
        var safeWorkspacePath = string.IsNullOrWhiteSpace(workspacePath) ? "(unknown path)" : workspacePath.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("# CEM Kimi Session");
        sb.AppendLine();
        sb.AppendLine($"Selected CEM profile: {profileLabel}");
        sb.AppendLine($"Workspace: {safeWorkspaceName}");
        sb.AppendLine($"Workspace path: {safeWorkspacePath}");
        sb.AppendLine();
        sb.AppendLine("## Guidance");
        sb.AppendLine("- Treat the workspace as the project root.");
        sb.AppendLine("- Prefer minimal, targeted changes.");
        sb.AppendLine("- Read repository instructions and local conventions before editing.");
        sb.AppendLine("- Ask before destructive or hard-to-reverse actions.");
        sb.AppendLine("- Explain any uncertainty clearly and keep the user informed.");

        if (!string.IsNullOrWhiteSpace(roleTemplatePath) && File.Exists(roleTemplatePath))
        {
            sb.AppendLine();
            sb.AppendLine("## Role Template");
            sb.AppendLine($"Source: {roleTemplatePath.Trim()}");
            sb.AppendLine();
            sb.AppendLine(File.ReadAllText(roleTemplatePath));
        }

        return sb.ToString();
    }

    public static string BuildAgentYaml() =>
        """
        version: 1
        agent:
          extend: default
          name: cem-kimi-session
          system_prompt_path: ./kimi-system.md
        """;
}
