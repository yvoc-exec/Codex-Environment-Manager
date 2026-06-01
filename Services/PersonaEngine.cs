using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public class PersonaEngine
{
    private const string PersonaManagedStart = "<!-- CODEX_ENV_MANAGER_PERSONA_START -->";
    private const string PersonaManagedEnd = "<!-- CODEX_ENV_MANAGER_PERSONA_END -->";
    private const string BurpManagedStart = "<!-- CODEX_ENV_MANAGER_BURP_START -->";
    private const string BurpManagedEnd = "<!-- CODEX_ENV_MANAGER_BURP_END -->";
    private const string LegacyManagedStart = "<!-- CODEX_ENV_MANAGER_START -->";
    private const string LegacyManagedEnd = "<!-- CODEX_ENV_MANAGER_END -->";

    private const string TomlManagedStart = "# CODEX_ENV_MANAGER_START";
    private const string TomlManagedEnd = "# CODEX_ENV_MANAGER_END";

    public static void EnsureAccountBaseConfig(string accountId)
    {
        var profilePath = JunctionManager.GetAccountProfilePath(accountId);
        Directory.CreateDirectory(profilePath);
        var configPath = Path.Combine(profilePath, "config.toml");
        var existing = File.Exists(configPath)
            ? File.ReadAllText(configPath)
            : "# Codex config" + Environment.NewLine;

        var updated = UpsertRootKeysPreserveManaged(existing, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cli_auth_credentials_store"] = "file"
        });
        var resolvedSandbox = ResolveWindowsSandbox(existing, "elevated");
        updated = UpsertTomlTableKey(updated, "windows", "sandbox", resolvedSandbox);

        updated = RemoveLegacyProfileContent(updated);
        ValidateManagedToml(updated);
        AtomicWriteText(configPath, updated);
    }

    public static void EnsureAccountRuntimeConfig(string accountId, string workspacePath, string windowsSandboxMode, bool trustWorkspace)
    {
        var profilePath = JunctionManager.GetAccountProfilePath(accountId);
        Directory.CreateDirectory(profilePath);
        var configPath = Path.Combine(profilePath, "config.toml");
        var existing = File.Exists(configPath)
            ? File.ReadAllText(configPath)
            : "# Codex config" + Environment.NewLine;

        var sandbox = ResolveWindowsSandbox(existing, windowsSandboxMode);

        var updated = UpsertRootKeysPreserveManaged(existing, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cli_auth_credentials_store"] = "file"
        });
        updated = UpsertTomlTableKey(updated, "windows", "sandbox", sandbox);

        if (trustWorkspace && !string.IsNullOrWhiteSpace(workspacePath))
        {
            var fullPath = Path.GetFullPath(workspacePath);
            var projectHeader = "projects." + QuoteTomlString(fullPath);
            updated = UpsertTomlTableKey(updated, projectHeader, "trust_level", "trusted");
        }

        updated = RemoveLegacyProfileContent(updated);
        ValidateManagedToml(updated);
        AtomicWriteText(configPath, updated);
    }

    public static string GetProfileName(Persona persona)
    {
        var name = new string((persona.Name ?? "persona")
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());
        while (name.Contains("__", StringComparison.Ordinal)) name = name.Replace("__", "_");
        name = name.Trim('_');
        if (string.IsNullOrWhiteSpace(name)) name = "persona";
        var suffix = string.IsNullOrWhiteSpace(persona.Id) ? "custom" : persona.Id[..Math.Min(8, persona.Id.Length)];
        return $"cem_{name}_{suffix}";
    }

    public void ApplyToAccount(Account acct, Persona persona, string? modelInstructionsFile = null, IEnumerable<Persona>? allPersonas = null)
    {
        var profilePath = JunctionManager.GetAccountProfilePath(acct.Id);
        Directory.CreateDirectory(profilePath);
        var configPath = Path.Combine(profilePath, "config.toml");

        try
        {
            var personas = (allPersonas ?? new[] { persona })
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (!personas.Any(p => string.Equals(p.Id, persona.Id, StringComparison.OrdinalIgnoreCase)))
                personas.Add(persona);

            foreach (var p in personas)
            {
                var instructionsFile = WriteProfileInstructionsFile(profilePath, acct, p);
                var profileName = GetProfileName(p);
                var values = BuildManagedProfileValues(p, instructionsFile);
                WriteProfileConfigFile(profilePath, profileName, values);
            }

            if (File.Exists(configPath))
            {
                var existing = File.ReadAllText(configPath);
                var cleaned = RemoveLegacyProfileContent(existing);
                if (!string.Equals(existing, cleaned, StringComparison.Ordinal))
                    AtomicWriteText(configPath, cleaned);
            }

            WriteCodexRoleCatalog(profilePath, personas);
            RemoveManagedAccountOverride(profilePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to update config.toml for account '{acct.Name}': {ex.Message}", ex);
        }
    }

    public string ApplyToWorkspace(Workspace ws, Account acct, Persona persona, bool writeAgentsMd = false)
    {
        string projectContext = "";
        if (!string.IsNullOrEmpty(ws.ProjectTemplate))
        {
            var templatePath = ResolveTemplatePath(Path.Combine("projects", ws.ProjectTemplate + ".md"));
            if (File.Exists(templatePath))
            {
                try { projectContext = File.ReadAllText(templatePath); }
                catch { /* template missing/unreadable is non-fatal */ }
            }
        }

        string personaContext = "";
        if (!string.IsNullOrEmpty(persona.AgentsTemplatePath))
        {
            var fullPath = ResolveTemplatePath(persona.AgentsTemplatePath);
            if (File.Exists(fullPath))
            {
                try { personaContext = File.ReadAllText(fullPath); }
                catch { /* template missing/unreadable is non-fatal */ }
            }
        }

        var managed = BuildManagedAgentsBlock(ws, acct, persona, projectContext, personaContext);
        var instructionPath = WriteGeneratedInstructions(ws, persona, managed);

        // Active CEM persona/profile switching is intentionally not written into workspace AGENTS.md.
        // Workspace AGENTS.md is shared by all sessions for a project; active role selection is handled by
        // Codex profile developer_instructions plus the Codex-level role catalog in CODEX_HOME/AGENTS.md.
        return instructionPath;
    }

    private static string WriteProfileInstructionsFile(string accountProfilePath, Account account, Persona persona)
    {
        var profileName = GetProfileName(persona);
        var dir = Path.Combine(accountProfilePath, "cem-profiles");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, profileName + ".instructions.md");
        var templateText = TryLoadRoleTemplate(persona);
        var roleBody = string.IsNullOrWhiteSpace(templateText)
            ? BuildBehaviorRules(persona).TrimEnd()
            : templateText.TrimEnd();

        var sb = new StringBuilder();
        sb.AppendLine("# CEM Active Profile Instructions");
        sb.AppendLine();
        sb.AppendLine("> DO NOT EDIT THIS FILE MANUALLY.");
        sb.AppendLine("> It is generated from the selected user-editable role template in `~/.codex-switcher/templates/personas/*.md`.");
        sb.AppendLine();
        sb.AppendLine("This file is selected by the active Codex profile via `model_instructions_file`.");
        sb.AppendLine("It is the active session role selector. Follow this file for profile behavior.");
        sb.AppendLine();
        sb.AppendLine("## Active CEM Profile");
        sb.AppendLine("- Account: " + (account.Name ?? account.Id) + " (" + account.Id + ")");
        sb.AppendLine("- CEM profile display name: " + (persona.Name ?? "Unnamed"));
        sb.AppendLine("- Codex profile name: `" + profileName + "`");
        sb.AppendLine("- Model: `" + GetConfigValue(persona, "model", "<default>") + "`");
        sb.AppendLine("- Reasoning effort: `" + GetConfigValue(persona, "model_reasoning_effort", "<default>") + "`");
        sb.AppendLine("- Sandbox mode: `" + GetConfigValue(persona, "sandbox_mode", "<default>") + "`");
        sb.AppendLine("- Approval policy: `" + GetConfigValue(persona, "approval_policy", "<default>") + "`");
        sb.AppendLine("- Approvals reviewer: `" + GetApprovalsReviewer(persona) + "`");
        sb.AppendLine();
        sb.AppendLine("## Identity / Verification Rule");
        sb.AppendLine("If asked what profile or mode you are running in, answer exactly from the Active CEM Profile section above.");
        sb.AppendLine("Do not infer another role from the model name alone.");
        sb.AppendLine();
        sb.AppendLine("## Active Role Rules");
        sb.AppendLine(roleBody);
        sb.AppendLine();
        sb.AppendLine("## Additional Context");
        sb.AppendLine("- Read `CEM_ACTIVE_CONTEXT.json` in CODEX_HOME when you need launch/account/workspace metadata.");
        sb.AppendLine("- The Codex-level `AGENTS.md` contains compact shared CEM guidance only.");
        sb.AppendLine("- Workspace `AGENTS.md`, if present, is project guidance and must not override this active CEM profile selection.");
        sb.AppendLine();

        AtomicWriteText(path, sb.ToString());
        return path;
    }

    private static string BuildProfileDeveloperInstructions(Persona persona)
    {
        var profileName = GetProfileName(persona);
        return "CEM ACTIVE PROFILE: " + (persona.Name ?? "Unnamed") +
               " | CODEX PROFILE: " + profileName +
               ". Use the profile-scoped model_instructions_file as the active role selector. " +
               "If asked what profile/mode you are in, answer exactly from the active CEM profile instructions file. " +
               "Do not infer another role from model name alone.";
    }

    public static Dictionary<string, string> BuildManagedProfileValues(Persona persona, string modelInstructionsFile)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in persona.ConfigOverrides)
            values[NormalizeConfigKey(kv.Key)] = kv.Value;

        values["developer_instructions"] = BuildProfileDeveloperInstructions(persona);
        values["model_instructions_file"] = modelInstructionsFile;
        values["approvals_reviewer"] = NormalizeApprovalsReviewer(persona.ApprovalsReviewer);
        return values;
    }

    public void RefreshRoleCatalogForAccount(Account acct, IEnumerable<Persona> allPersonas, Action<string>? warn = null)
    {
        var profilePath = JunctionManager.GetAccountProfilePath(acct.Id);
        Directory.CreateDirectory(profilePath);

        var personas = allPersonas
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var configPath = Path.Combine(profilePath, "config.toml");

        foreach (var p in personas)
        {
            var instructionsFile = WriteProfileInstructionsFile(profilePath, acct, p);
            var profileName = GetProfileName(p);
            var values = BuildManagedProfileValues(p, instructionsFile);
            WriteProfileConfigFile(profilePath, profileName, values);
        }

        if (File.Exists(configPath))
        {
            var existing = File.ReadAllText(configPath);
            var cleaned = RemoveLegacyProfileContent(existing);
            if (!string.Equals(existing, cleaned, StringComparison.Ordinal))
                AtomicWriteText(configPath, cleaned);
        }

        WriteCodexRoleCatalog(profilePath, personas);
        RemoveManagedAccountOverride(profilePath);
    }

    private static string ReadRootProfile(string configPath)
    {
        if (!File.Exists(configPath)) return "";
        foreach (var raw in SplitLines(File.ReadAllText(configPath)))
        {
            var line = raw.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal)) break;
            var match = Regex.Match(line, "^profile\\s*=\\s*\\\"([^\\\"]+)\\\"");
            if (match.Success) return match.Groups[1].Value;
        }
        return "";
    }

    public static string ResolveManagedActiveProfileName(string? storedRootProfile, IReadOnlyCollection<Persona> personas, Action<string>? warn = null)
    {
        var profileNames = personas
            .Select(GetProfileName)
            .ToList();

        if (!string.IsNullOrWhiteSpace(storedRootProfile) &&
            profileNames.Any(name => string.Equals(name, storedRootProfile, StringComparison.OrdinalIgnoreCase)))
        {
            return storedRootProfile;
        }

        if (profileNames.Count > 0)
        {
            var fallback = profileNames[0];
            if (!string.IsNullOrWhiteSpace(storedRootProfile))
                warn?.Invoke($"Stored root Codex profile '{storedRootProfile}' was missing after role refresh; falling back to '{fallback}'.");
            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(storedRootProfile))
            warn?.Invoke($"Stored root Codex profile '{storedRootProfile}' was missing and no generated profiles were available; clearing the root profile entry.");

        return "";
    }

    private static void WriteCodexRoleCatalog(string accountProfilePath, IEnumerable<Persona> personas)
    {
        // Pass 46: AGENTS.md is compact shared account guidance only.
        // Full role behavior belongs only in generated cem-profiles/*.instructions.md files.
        var agentsPath = Path.Combine(accountProfilePath, "AGENTS.md");
        var existing = File.Exists(agentsPath) ? File.ReadAllText(agentsPath) : "";

        var guidance = BuildCompactAccountGuidanceBlock(personas);
        var updated = ReplaceDelimitedBlock(existing, guidance, PersonaManagedStart, PersonaManagedEnd)
                      ?? (string.IsNullOrWhiteSpace(existing)
                          ? guidance
                          : existing.TrimEnd() + Environment.NewLine + Environment.NewLine + guidance);

        AtomicWriteText(agentsPath, updated);
    }

    private static string BuildCompactAccountGuidanceBlock(IEnumerable<Persona> personas)
    {
        var profileNames = personas
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => "- `" + GetProfileName(p) + "` -> " + (p.Name ?? "Unnamed"))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(PersonaManagedStart);
        sb.AppendLine("# Codex Environment Manager Account Guidance");
        sb.AppendLine();
        sb.AppendLine("> DO NOT EDIT THIS MANAGED BLOCK MANUALLY.");
        sb.AppendLine("> It is generated by Codex Environment Manager.");
        sb.AppendLine();
        sb.AppendLine("This Codex home is managed by Codex Environment Manager (CEM).");
        sb.AppendLine();
        sb.AppendLine("## Source of Truth");
        sb.AppendLine();
        sb.AppendLine("- User-editable role behavior lives in `~/.codex-switcher/templates/personas/*.md`.");
        sb.AppendLine("- Generated active profile instructions live in `cem-profiles/*.instructions.md`.");
        sb.AppendLine("- `config.toml` points each Codex profile to its generated `model_instructions_file`.");
        sb.AppendLine("- This `AGENTS.md` file is compact shared guidance only. It must not duplicate full role templates.");
        sb.AppendLine();
        sb.AppendLine("## Runtime Rules");
        sb.AppendLine();
        sb.AppendLine("- Follow the selected Codex profile and its `model_instructions_file` as the active role source of truth.");
        sb.AppendLine("- Do not infer the active role from model name alone.");
        sb.AppendLine("- Read `CEM_ACTIVE_CONTEXT.json` when account/profile/workspace metadata is needed.");
        sb.AppendLine("- Workspace `AGENTS.md` files, if present, are project-specific guidance and should not override the active CEM profile selection.");
        sb.AppendLine();
        sb.AppendLine("## Managed Profiles");
        sb.AppendLine();
        sb.AppendLine("The profile list below is informational only. Full behavior is in each profile instruction file.");
        sb.AppendLine();
        if (profileNames.Count == 0)
        {
            sb.AppendLine("- No CEM profiles found.");
        }
        else
        {
            foreach (var line in profileNames)
                sb.AppendLine(line);
        }
        sb.AppendLine();
        sb.AppendLine(PersonaManagedEnd);
        return sb.ToString();
    }

    private static string TryLoadRoleTemplate(Persona persona)
    {
        if (string.IsNullOrWhiteSpace(persona.AgentsTemplatePath)) return "";
        try
        {
            var templatePath = ResolveTemplatePath(persona.AgentsTemplatePath);
            if (!File.Exists(templatePath)) return "";
            return File.ReadAllText(templatePath);
        }
        catch
        {
            return "";
        }
    }

    private static string GetConfigValue(Persona p, string key, string fallback)
    {
        if (p.ConfigOverrides.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        return fallback;
    }

    private static void RemoveManagedAccountOverride(string accountProfilePath)
    {
        var overridePath = Path.Combine(accountProfilePath, "AGENTS.override.md");
        if (!File.Exists(overridePath)) return;

        try
        {
            var text = File.ReadAllText(overridePath);
            if (text.Contains("Codex Environment Manager Active Instructions", StringComparison.Ordinal) ||
                text.Contains("CEM_ACTIVE_CONTEXT.json", StringComparison.Ordinal))
            {
                File.Delete(overridePath);
            }
        }
        catch
        {
            // Non-fatal cleanup.
        }
    }

    private static void WriteAccountPersonaOverride(string accountProfilePath, Account acct, Persona persona, string? generatedInstructionsFile)
    {
        var overridePath = Path.Combine(accountProfilePath, "AGENTS.override.md");
        var profileName = GetProfileName(persona);
        var sb = new StringBuilder();
        sb.AppendLine("# Codex Environment Manager Active Instructions");
        sb.AppendLine();
        sb.AppendLine("These instructions are generated by Codex Environment Manager for the currently selected account/profile/workspace launch.");
        sb.AppendLine("They are behavioral constraints, not a fake runtime identity claim. Codex may still report its native collaboration mode as Default; that does not override the rules below.");
        sb.AppendLine();
        sb.AppendLine("## Active Launch Context");
        sb.AppendLine($"- CEM account: {acct.Name} ({acct.Type})");
        sb.AppendLine($"- CEM profile/persona label: {persona.Name}");
        sb.AppendLine($"- Codex profile: {profileName}");
        sb.AppendLine($"- Active context file: {Path.Combine(accountProfilePath, "CEM_ACTIVE_CONTEXT.json")}");
        sb.AppendLine();
        sb.AppendLine("## Behavioral Rules for This CEM Profile");
        sb.AppendLine(BuildBehaviorRules(persona));
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(generatedInstructionsFile) && File.Exists(generatedInstructionsFile))
        {
            sb.AppendLine("## Generated Workspace Instructions");
            sb.AppendLine();
            try
            {
                sb.AppendLine(File.ReadAllText(generatedInstructionsFile).TrimEnd());
            }
            catch
            {
                sb.AppendLine($"Generated instruction file was unavailable: {generatedInstructionsFile}");
            }
        }

        AtomicWriteText(overridePath, sb.ToString());
    }

    private static string BuildBehaviorRules(Persona persona)
    {
        var name = persona.Name ?? string.Empty;
        if (name.Contains("planner", StringComparison.OrdinalIgnoreCase))
        {
            return "- Act as a planning and architecture assistant.\n" +
                   "- Do not edit, create, delete, or overwrite files.\n" +
                   "- Do not run mutating commands. Prefer reading/inspection only.\n" +
                   "- Produce plans, risk notes, file-by-file implementation steps, and review checklists.\n" +
                   "- If the user asks for implementation, provide an implementation plan or ask them to switch to Implementor.";
        }

        if (name.Contains("review", StringComparison.OrdinalIgnoreCase))
        {
            return "- Act as a code reviewer and security/quality auditor.\n" +
                   "- Inspect files and reason about bugs, regressions, security issues, and architecture gaps.\n" +
                   "- Do not implement changes unless the user explicitly asks you to switch from review into implementation.\n" +
                   "- Prefer evidence: cite files, functions, and exact behavior.\n" +
                   "- Produce prioritized findings and safe fix recommendations.";
        }

        if (name.Contains("implement", StringComparison.OrdinalIgnoreCase) || name.Contains("executor", StringComparison.OrdinalIgnoreCase))
        {
            return "- Act as an implementation agent.\n" +
                   "- Modify files only inside the selected workspace and follow the requested plan.\n" +
                   "- Keep changes focused, reversible, and aligned with existing project style.\n" +
                   "- Run relevant checks/tests when appropriate under the active sandbox and approval policy.\n" +
                   "- Report changed files, validation steps, and remaining risks.";
        }

        return "- Follow the selected CEM profile configuration and the generated workspace instructions.\n" +
               "- Do not assume permissions beyond the active Codex profile sandbox and approval policy.\n" +
               "- Keep actions scoped to the selected workspace.";
    }

    public static void UpsertManagedSection(string filePath, string title, string body)
    {
        var sectionId = SanitizeMarkerId(title);
        var start = $"<!-- CODEX_ENV_MANAGER_BURP_START:{sectionId} -->";
        var end = $"<!-- CODEX_ENV_MANAGER_BURP_END:{sectionId} -->";

        var section = new StringBuilder();
        section.AppendLine(start);
        section.AppendLine($"## {title}");
        section.AppendLine();
        section.AppendLine(body.TrimEnd());
        section.AppendLine(end);

        var existing = File.Exists(filePath) ? File.ReadAllText(filePath) : "";
        var updated = ReplaceDelimitedBlock(existing, section.ToString(), start, end)
                      ?? ReplaceDelimitedBlock(existing, section.ToString(), BurpManagedStart, BurpManagedEnd)
                      ?? (string.IsNullOrWhiteSpace(existing)
                          ? section.ToString()
                          : existing.TrimEnd() + Environment.NewLine + Environment.NewLine + section);
        AtomicWriteText(filePath, updated);
    }

    // Backwards-compatible method name used by older UI code.
    public static void AppendManagedSection(string filePath, string title, string body) =>
        UpsertManagedSection(filePath, title, body);

    private static void UpsertPersonaBlockInAgents(Workspace ws, string managed)
    {
        var agentsPath = Path.Combine(ws.Path, "AGENTS.md");
        Directory.CreateDirectory(ws.Path);
        var existing = File.Exists(agentsPath) ? File.ReadAllText(agentsPath) : "";
        var updated = ReplaceOrAppendPersonaBlock(existing, managed);
        if (!string.Equals(existing, updated, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(existing))
                AgentsBackupGuard.BackupIfExists(ws.Path);
            AtomicWriteText(agentsPath, updated);
        }
    }

    private static string WriteGeneratedInstructions(Workspace ws, Persona persona, string managed)
    {
        var dir = Path.Combine(JunctionManager.SwitcherDir, "generated", "workspaces", SanitizePathPart(ws.Id), SanitizePathPart(persona.Id));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "instructions.md");
        AtomicWriteText(path, managed);
        return path;
    }

    private static string SanitizePathPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }

    private static string ResolveTemplatePath(string templatePath)
    {
        if (Path.IsPathRooted(templatePath)) return templatePath;

        var trimmed = templatePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (trimmed.StartsWith("Templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[("Templates" + Path.DirectorySeparatorChar).Length..];

        var writable = Path.Combine(JunctionManager.SwitcherDir, "templates", trimmed);
        if (File.Exists(writable)) return writable;

        return Path.Combine(AppContext.BaseDirectory, "Templates", trimmed);
    }

    private static string BuildManagedAgentsBlock(Workspace ws, Account acct, Persona persona, string projectContext, string personaContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine(PersonaManagedStart);
        sb.AppendLine($"# Codex Context — {persona.Name} Mode");
        sb.AppendLine($"# Account: {acct.Name} ({acct.Type})");
        sb.AppendLine($"# Workspace: {ws.Name}");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(projectContext))
        {
            sb.AppendLine("## Project Context");
            sb.AppendLine(projectContext.TrimEnd());
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(personaContext))
        {
            sb.AppendLine($"## Persona: {persona.Name}");
            sb.AppendLine(personaContext.TrimEnd());
            sb.AppendLine();
        }
        sb.AppendLine("## Session Instructions");
        sb.AppendLine("- Preserve all previous context from this workspace.");
        sb.AppendLine("- Reference existing files before modifying.");
        sb.AppendLine("- Do not break existing functionality.");
        sb.AppendLine(PersonaManagedEnd);
        return sb.ToString();
    }

    private static string ReplaceOrAppendPersonaBlock(string existing, string managed)
    {
        var replaced = ReplaceDelimitedBlock(existing, managed, PersonaManagedStart, PersonaManagedEnd);
        if (replaced != null) return replaced;

        var legacyStart = existing.IndexOf(LegacyManagedStart, StringComparison.Ordinal);
        var legacyEnd = existing.IndexOf(LegacyManagedEnd, StringComparison.Ordinal);
        if (legacyStart >= 0 && legacyEnd >= legacyStart)
        {
            var lineEnd = existing.IndexOfAny(new[] { '\r', '\n' }, legacyStart);
            var firstLine = lineEnd >= 0 ? existing[legacyStart..lineEnd] : existing[legacyStart..];
            if (string.Equals(firstLine.Trim(), LegacyManagedStart, StringComparison.Ordinal))
            {
                return ReplaceDelimitedBlock(existing, managed, LegacyManagedStart, LegacyManagedEnd)!;
            }
        }

        if (string.IsNullOrWhiteSpace(existing)) return managed;
        return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + managed;
    }

    private static string? ReplaceDelimitedBlock(string existing, string replacement, string startMarker, string endMarker)
    {
        var start = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var end = existing.IndexOf(endMarker, StringComparison.Ordinal);
        if (start < 0 || end < start) return null;

        end += endMarker.Length;
        while (end < existing.Length && (existing[end] == '\r' || existing[end] == '\n' || existing[end] == ' ' || existing[end] == '\t')) end++;
        return existing[..start].TrimEnd() + Environment.NewLine + Environment.NewLine + replacement.TrimEnd() + Environment.NewLine + existing[end..].TrimStart();
    }

    private static string RemoveDelimitedBlock(string existing, string startMarker, string endMarker)
    {
        var start = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var end = existing.IndexOf(endMarker, StringComparison.Ordinal);
        if (start < 0 || end < start) return existing;

        end += endMarker.Length;
        while (end < existing.Length && (existing[end] == '\r' || existing[end] == '\n' || existing[end] == ' ' || existing[end] == '\t')) end++;
        return (existing[..start].TrimEnd() + Environment.NewLine + existing[end..].TrimStart()).Trim();
    }

    private static string RemoveCemRootProfileKey(string existing)
    {
        var lines = SplitLines(existing);
        var updated = lines.Where(line => !Regex.IsMatch(line.Trim(), "^profile\\s*=\\s*\\\"cem_", RegexOptions.IgnoreCase)).ToList();
        var text = string.Join(Environment.NewLine, updated).Trim();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text + Environment.NewLine;
    }

    private static void WriteProfileConfigFile(string accountProfilePath, string profileName, Dictionary<string, string> values)
    {
        var path = Path.Combine(accountProfilePath, profileName + ".config.toml");
        var sb = new StringBuilder();
        sb.AppendLine($"# Profile config for {profileName}");
        sb.AppendLine("# Generated by Codex Environment Manager. Edit personas in the app/templates, not this file.");
        sb.AppendLine();
        foreach (var kv in values.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"{NormalizeConfigKey(kv.Key)} = {FormatTomlValue(kv.Value)}");
        }
        AtomicWriteText(path, sb.ToString());
    }

    private static string RemoveLegacyProfileContent(string existing)
    {
        var cleaned = RemoveLegacyManagedTomlBlocks(existing);
        cleaned = RemoveManagedProfileSections(cleaned);
        cleaned = RemoveCemRootProfileKey(cleaned);
        return cleaned.Trim();
    }

    private static string RemoveLegacyManagedTomlBlocks(string existing)
    {
        var cleaned = existing;
        while (true)
        {
            var next = RemoveDelimitedBlock(cleaned, TomlManagedStart, TomlManagedEnd);
            if (string.Equals(next, cleaned, StringComparison.Ordinal)) return cleaned;
            cleaned = next;
        }
    }

    private static string RemoveManagedProfileSections(string existing)
    {
        var lines = SplitLines(existing);
        var output = new List<string>();
        var skipping = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^\[profiles\.cem_[A-Za-z0-9_]+\]\s*$"))
            {
                skipping = true;
                continue;
            }

            if (skipping && trimmed.StartsWith("[", StringComparison.Ordinal))
                skipping = false;

            if (!skipping) output.Add(line);
        }

        return string.Join(Environment.NewLine, output).Trim();
    }

    public static void ValidateAccountProfileExists(string accountId, string profileName)
    {
        var accountPath = JunctionManager.GetAccountProfilePath(accountId);
        var profileConfigPath = Path.Combine(accountPath, profileName + ".config.toml");
        if (!File.Exists(profileConfigPath))
        {
            throw new InvalidOperationException(
                $"Selected Codex profile config '{profileConfigPath}' does not exist. " +
                "Launch aborted before Codex start to prevent profile mismatch. " +
                "Try Refresh all account configs, then launch again.");
        }

        var profileInstructions = Path.Combine(accountPath, "cem-profiles", profileName + ".instructions.md");
        if (!File.Exists(profileInstructions))
        {
            throw new InvalidOperationException(
                $"Selected Codex profile '{profileName}' is missing its active instruction file: {profileInstructions}");
        }
    }

    public static void ValidateAccountBaseConfigClean(string accountId)
    {
        var accountPath = JunctionManager.GetAccountProfilePath(accountId);
        var configPath = Path.Combine(accountPath, "config.toml");
        if (!File.Exists(configPath)) return;

        var toml = File.ReadAllText(configPath);
        if (Regex.IsMatch(toml, @"^profile\s*=\s*""cem_", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                $"Account config.toml still contains a legacy CEM profile selector. " +
                "Run Refresh Instructions to clean the config.");
        }

        if (Regex.IsMatch(toml, @"^\[profiles\.cem_", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                $"Account config.toml still contains legacy CEM [profiles.*] tables. " +
                "Run Refresh Instructions to clean the config.");
        }
    }

    private const string DesktopProfileStart = "# CODEX_ENV_MANAGER_DESKTOP_PROFILE_START";
    private const string DesktopProfileEnd = "# CODEX_ENV_MANAGER_DESKTOP_PROFILE_END";

    public static void MaterializeProfileForDesktopLaunch(string accountId, Persona persona)
    {
        var profilePath = JunctionManager.GetAccountProfilePath(accountId);
        var configPath = Path.Combine(profilePath, "config.toml");
        var profileName = GetProfileName(persona);
        var profileConfigPath = Path.Combine(profilePath, profileName + ".config.toml");

        if (!File.Exists(profileConfigPath))
            throw new InvalidOperationException(
                $"Selected Codex profile config '{profileConfigPath}' does not exist. " +
                "Launch aborted before Codex start to prevent profile mismatch.");

        var profileValues = ParseSimpleProfileConfig(File.ReadAllText(profileConfigPath));
        if (profileValues.Count == 0)
            throw new InvalidOperationException(
                $"Selected Codex profile config '{profileConfigPath}' is empty or unreadable.");

        var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : "# Codex config" + Environment.NewLine;

        // Remove previous desktop materialization block
        var cleaned = RemoveDelimitedBlock(existing, DesktopProfileStart, DesktopProfileEnd);

        // Remove root keys that will be managed by materialization to avoid duplicates
        cleaned = RemoveRootKeys(cleaned, new HashSet<string>(profileValues.Keys, StringComparer.OrdinalIgnoreCase));

        // Build materialized block
        var sb = new StringBuilder();
        sb.AppendLine(DesktopProfileStart);
        sb.AppendLine("# Active Desktop profile materialized by CEM because `codex app` does not support --profile.");
        foreach (var kv in profileValues.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"{NormalizeConfigKey(kv.Key)} = {FormatTomlValue(kv.Value)}");
        }
        sb.AppendLine(DesktopProfileEnd);

        var materializedBlock = sb.ToString().TrimEnd();
        var body = cleaned.Trim();
        var updated = string.IsNullOrWhiteSpace(body)
            ? materializedBlock + Environment.NewLine
            : materializedBlock + Environment.NewLine + Environment.NewLine + body + Environment.NewLine;

        AtomicWriteText(configPath, updated);
    }

    private static Dictionary<string, string> ParseSimpleProfileConfig(string toml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in SplitLines(toml))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            // Unquote outer TOML string quotes so FormatTomlValue does not double-quote
            if (value.Length >= 2 && value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
                value = value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
            result[key] = value;
        }
        return result;
    }

    private static string RemoveRootKeys(string existing, HashSet<string> rootKeySet)
    {
        var lines = SplitLines(existing);
        var output = new List<string>();
        var inRoot = true;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (inRoot && trimmed.StartsWith("[", StringComparison.Ordinal))
                inRoot = false;

            if (inRoot)
            {
                var match = Regex.Match(trimmed, @"^([A-Za-z0-9_.-]+)\s*=");
                if (match.Success && rootKeySet.Contains(match.Groups[1].Value))
                    continue;
            }

            output.Add(line);
        }

        return string.Join(Environment.NewLine, output).Trim();
    }

    private static string UpsertRootKeysPreserveManaged(string existing, Dictionary<string, string> rootKeys)
    {
        var lines = SplitLines(existing);
        var output = new List<string>();
        var inRoot = true;
        var rootKeySet = new HashSet<string>(rootKeys.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (inRoot && trimmed.StartsWith("[", StringComparison.Ordinal))
                inRoot = false;

            if (inRoot)
            {
                var match = Regex.Match(trimmed, @"^([A-Za-z0-9_.-]+)\s*=");
                if (match.Success && rootKeySet.Contains(match.Groups[1].Value))
                    continue;
            }

            output.Add(line);
        }

        var rootBlock = string.Join(Environment.NewLine, rootKeys
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key} = {FormatTomlValue(kv.Value)}"));

        var body = string.Join(Environment.NewLine, output).Trim();
        if (string.IsNullOrWhiteSpace(body)) return rootBlock + Environment.NewLine;
        return rootBlock + Environment.NewLine + Environment.NewLine + body + Environment.NewLine;
    }

    private static string UpsertRootKeys(string existing, Dictionary<string, string> rootKeys)
    {
        var lines = SplitLines(RemoveLegacyManagedTomlBlocks(existing));
        var output = new List<string>();
        var inRoot = true;
        var rootKeySet = new HashSet<string>(rootKeys.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (inRoot && trimmed.StartsWith("[", StringComparison.Ordinal))
                inRoot = false;

            if (inRoot)
            {
                var match = Regex.Match(trimmed, @"^([A-Za-z0-9_.-]+)\s*=");
                if (match.Success && rootKeySet.Contains(match.Groups[1].Value))
                    continue;
            }

            output.Add(line);
        }

        var rootBlock = string.Join(Environment.NewLine, rootKeys
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key} = {FormatTomlValue(kv.Value)}"));

        var body = string.Join(Environment.NewLine, output).Trim();
        if (string.IsNullOrWhiteSpace(body)) return rootBlock + Environment.NewLine;
        return rootBlock + Environment.NewLine + Environment.NewLine + body + Environment.NewLine;
    }

    private static string ResolveWindowsSandbox(string existing, string requested)
    {
        var current = GetTomlTableKey(existing, "windows", "sandbox");
        if (string.Equals(current, "elevated", StringComparison.OrdinalIgnoreCase))
            return "elevated";
        if (string.Equals(current, "unelevated", StringComparison.OrdinalIgnoreCase))
            return "unelevated";

        return string.Equals(requested, "unelevated", StringComparison.OrdinalIgnoreCase)
            ? "unelevated"
            : "elevated";
    }

    private static string GetTomlTableKey(string existing, string tableHeader, string key)
    {
        var lines = SplitLines(existing).ToList();
        var header = "[" + tableHeader + "]";
        var start = -1;
        var end = lines.Count;

        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.Ordinal))
            {
                start = i;
                for (var j = i + 1; j < lines.Count; j++)
                {
                    var trimmed = lines[j].Trim();
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        end = j;
                        break;
                    }
                }
                break;
            }
        }

        if (start < 0) return "";
        var keyRegex = new Regex("^\\s*" + Regex.Escape(key) + "\\s*=\\s*(.+?)\\s*$", RegexOptions.IgnoreCase);
        for (var i = start + 1; i < end; i++)
        {
            var match = keyRegex.Match(lines[i]);
            if (!match.Success) continue;
            var raw = match.Groups[1].Value.Trim();
            if (raw.Length >= 2 && raw.StartsWith("\"", StringComparison.Ordinal) && raw.EndsWith("\"", StringComparison.Ordinal))
                raw = raw[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
            return raw;
        }

        return "";
    }

    private static string UpsertTomlTableKey(string existing, string tableHeader, string key, string value)
    {
        var lines = SplitLines(existing).ToList();
        var header = "[" + tableHeader + "]";
        var keyRegex = new Regex("^\\s*" + Regex.Escape(key) + "\\s*=", RegexOptions.IgnoreCase);
        var start = -1;
        var end = lines.Count;

        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.Ordinal))
            {
                start = i;
                for (var j = i + 1; j < lines.Count; j++)
                {
                    var trimmed = lines[j].Trim();
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        end = j;
                        break;
                    }
                }
                break;
            }
        }

        var assignment = key + " = " + FormatTomlValue(value);

        if (start < 0)
        {
            var body = string.Join(Environment.NewLine, lines).TrimEnd();
            if (!string.IsNullOrWhiteSpace(body))
                body += Environment.NewLine + Environment.NewLine;
            return body + header + Environment.NewLine + assignment + Environment.NewLine;
        }

        for (var i = start + 1; i < end; i++)
        {
            if (keyRegex.IsMatch(lines[i]))
            {
                lines[i] = assignment;
                return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
            }
        }

        lines.Insert(start + 1, assignment);
        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    private static string[] SplitLines(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static void ValidateManagedToml(string toml)
    {
        var lines = SplitLines(toml);
        var rootKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inRoot = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inRoot = false;
                continue;
            }

            var match = Regex.Match(line, @"^([A-Za-z0-9_.-]+)\s*=");
            if (!match.Success) continue;

            var key = match.Groups[1].Value;
            if (inRoot)
            {
                if (!rootKeys.Add(key))
                    throw new InvalidOperationException($"Generated config.toml contains duplicate root key: {key}");
            }
        }
    }

    private static string SanitizeMarkerId(string value)
    {
        var cleaned = new string((value ?? "section").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (cleaned.Contains("--", StringComparison.Ordinal)) cleaned = cleaned.Replace("--", "-");
        return string.IsNullOrWhiteSpace(cleaned) ? "section" : cleaned;
    }

    private static string AppendBlockAtEnd(string existing, string block)
    {
        if (string.IsNullOrWhiteSpace(existing)) return block.TrimEnd() + Environment.NewLine;
        return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + block.TrimEnd() + Environment.NewLine;
    }

    private static string GetApprovalsReviewer(Persona persona) =>
        NormalizeApprovalsReviewer(persona.ApprovalsReviewer);

    private static string NormalizeApprovalsReviewer(string? reviewer) =>
        string.Equals(reviewer?.Trim(), "auto_review", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reviewer?.Trim(), "guardian_subagent", StringComparison.OrdinalIgnoreCase)
            ? "auto_review"
            : "user";

    private static string NormalizeConfigKey(string key) => key switch
    {
        "reasoning_effort" => "model_reasoning_effort",
        "approval" => "approval_policy",
        "sandbox" => "sandbox_mode",
        _ => key
    };

    private static string FormatTomlValue(string value)
    {
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)) return "true";
        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return "false";
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)) return trimmed;
        if (decimal.TryParse(trimmed, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _)) return trimmed;
        return QuoteTomlString(value);
    }

    private static string QuoteTomlString(string value) => $"\"{EscapeToml(value)}\"";

    private static string EscapeToml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void AtomicWriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, Encoding.UTF8);
        File.Move(temp, path, overwrite: true);
    }
}
