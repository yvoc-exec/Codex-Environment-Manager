using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public class ConfigService
{
    private readonly string _baseDir;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Mutex ConfigMutex = new(false, @"Local\CodexEnvironmentManager_Config");

    public ConfigService() : this(null)
    {
    }

    public ConfigService(string? baseDir)
    {
        _baseDir = string.IsNullOrWhiteSpace(baseDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex-switcher")
            : baseDir;
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(Path.Combine(_baseDir, "accounts"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "snapshots"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "generated"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "templates", "personas"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "templates", "projects"));
        SeedWritableTemplates();
    }

    private void SeedWritableTemplates()
    {
        var bundledTemplates = Path.Combine(AppContext.BaseDirectory, "Templates");
        if (!Directory.Exists(bundledTemplates)) return;

        foreach (var source in Directory.GetFiles(bundledTemplates, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(bundledTemplates, source);
            var target = Path.Combine(TemplatesDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target))
                File.Copy(source, target);
        }
    }

    public string BaseDir => _baseDir;
    public string TemplatesDir => Path.Combine(_baseDir, "templates");
    public string GeneratedDir => Path.Combine(_baseDir, "generated");

    private string PathFor(string name) => Path.Combine(_baseDir, name + ".json");

    public List<T> LoadList<T>(string name) where T : new()
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return new List<T>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (Exception ex)
        {
            var backup = path + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss");
            try { File.Move(path, backup); } catch { }
            throw new InvalidOperationException($"Configuration file '{name}.json' was corrupted. It has been backed up to '{backup}'. Please reconfigure.", ex);
        }
    }

    public void SaveList<T>(string name, List<T> list)
    {
        var path = PathFor(name);
        var json = JsonSerializer.Serialize(list, JsonOptions);
        WithConfigLock(() => AtomicWriteText(path, json));
    }

    public void EnsureDefaults()
    {
        var personas = LoadList<Persona>("personas");
        var changed = false;

        changed |= MigratePersonas(personas);
        changed |= UpsertCanonicalPersona(personas,
            "Planner+Reviewer GPT 5.4 Medium",
            "🧠🔍",
            "Templates/personas/planner_reviewer_gpt54_medium.md",
            "gpt-5.4",
            "medium",
            "read-only",
            "on-request",
            "user",
            "plan_review");

        changed |= UpsertCanonicalPersona(personas,
            "Planner+Reviewer GPT 5.4 High",
            "🧠🔍",
            "Templates/personas/planner_reviewer_gpt54_high.md",
            "gpt-5.4",
            "high",
            "read-only",
            "on-request",
            "user",
            "plan_review");

        changed |= UpsertCanonicalPersona(personas,
            "Planner+Reviewer GPT 5.5 High",
            "🧠🔍",
            "Templates/personas/planner_reviewer_gpt55_high.md",
            "gpt-5.5",
            "high",
            "read-only",
            "on-request",
            "user",
            "plan_review");

        changed |= UpsertCanonicalPersona(personas,
            "Reviewer GPT 5.5 Extra High",
            "🔍",
            "Templates/personas/reviewer_gpt55_xhigh.md",
            "gpt-5.5",
            "xhigh",
            "read-only",
            "on-request",
            "user",
            "review");

        changed |= UpsertCanonicalPersona(personas,
            "Implementor GPT 5.4 Mini High",
            "🔨",
            "Templates/personas/implementor_gpt54mini_high.md",
            "gpt-5.4-mini",
            "high",
            "workspace-write",
            "on-request",
            "user",
            "implement");

        changed |= RemoveDuplicatePersonasByName(personas);

        if (changed)
            SaveList("personas", personas);
    }

    private static bool MigratePersonas(List<Persona> personas)
    {
        var changed = false;
        changed |= RenamePersonaIfPresent(personas, "Planner", "Planner+Reviewer GPT 5.4 High", "🧠🔍");
        changed |= RenamePersonaIfPresent(personas, "Reviewer", "Reviewer GPT 5.5 Extra High", "🔍");
        changed |= RenamePersonaIfPresent(personas, "Reviewer GPT 5.5 Medium", "Reviewer GPT 5.5 Extra High", "🔍");
        changed |= RenamePersonaIfPresent(personas, "Implementor", "Implementor GPT 5.4 Mini High", "🔨");

        foreach (var p in personas)
        {
            changed |= RenameConfigKey(p.ConfigOverrides, "reasoning_effort", "model_reasoning_effort");
            changed |= RenameConfigKey(p.ConfigOverrides, "approval", "approval_policy");
            changed |= RenameConfigKey(p.ConfigOverrides, "sandbox", "sandbox_mode");

            if (p.ConfigOverrides.TryGetValue("approvals_reviewer", out var reviewerOverride))
            {
                var normalizedReviewer = NormalizeApprovalsReviewer(reviewerOverride);
                if (!string.Equals(p.ApprovalsReviewer, normalizedReviewer, StringComparison.OrdinalIgnoreCase))
                {
                    p.ApprovalsReviewer = normalizedReviewer;
                    changed = true;
                }
                p.ConfigOverrides.Remove("approvals_reviewer");
                changed = true;
            }

            var normalized = NormalizeApprovalsReviewer(p.ApprovalsReviewer);
            if (!string.Equals(p.ApprovalsReviewer, normalized, StringComparison.OrdinalIgnoreCase))
            {
                p.ApprovalsReviewer = normalized;
                changed = true;
            }

            if (!p.ConfigOverrides.ContainsKey("approval_policy"))
            {
                p.ConfigOverrides["approval_policy"] = "on-request";
                changed = true;
            }
            if (!p.ConfigOverrides.ContainsKey("sandbox_mode"))
            {
                p.ConfigOverrides["sandbox_mode"] = "read-only";
                changed = true;
            }

            if (StripProfileControlledCliArgs(p.CliArgs))
                changed = true;
        }

        return changed;
    }

    private static bool UpsertCanonicalPersona(
        List<Persona> personas,
        string name,
        string icon,
        string templatePath,
        string model,
        string reasoning,
        string sandbox,
        string approval,
        string approvalsReviewer,
        string mode)
    {
        var changed = false;
        var existing = personas.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = new Persona
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Icon = icon,
                AgentsTemplatePath = templatePath,
                ConfigOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                EnvVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                CliArgs = new List<string>()
            };
            personas.Add(existing);
            changed = true;
        }

        changed |= SetIfDifferent(existing, x => x.Icon, v => existing.Icon = v, icon);
        changed |= SetIfDifferent(existing, x => x.AgentsTemplatePath, v => existing.AgentsTemplatePath = v, templatePath);
        changed |= SetConfig(existing, "model", model);
        changed |= SetConfig(existing, "model_reasoning_effort", reasoning);
        changed |= SetConfig(existing, "sandbox_mode", sandbox);
        changed |= SetConfig(existing, "approval_policy", approval);
        changed |= SetIfDifferent(existing, x => x.ApprovalsReviewer, v => existing.ApprovalsReviewer = v, NormalizeApprovalsReviewer(approvalsReviewer));

        if (!existing.EnvVars.TryGetValue("CODEX_MODE", out var currentMode) ||
            !string.Equals(currentMode, mode, StringComparison.Ordinal))
        {
            existing.EnvVars["CODEX_MODE"] = mode;
            changed = true;
        }

        if (StripProfileControlledCliArgs(existing.CliArgs))
            changed = true;

        return changed;
    }

    private static bool RenamePersonaIfPresent(List<Persona> personas, string oldName, string newName, string icon)
    {
        var existing = personas.FirstOrDefault(p => string.Equals(p.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return false;

        existing.Name = newName;
        if (!string.IsNullOrWhiteSpace(icon)) existing.Icon = icon;
        return true;
    }

    private static bool SetIfDifferent(Persona persona, Func<Persona, string> getter, Action<string> setter, string value)
    {
        if (string.Equals(getter(persona), value, StringComparison.Ordinal)) return false;
        setter(value);
        return true;
    }

    private static bool SetConfig(Persona persona, string key, string value)
    {
        if (persona.ConfigOverrides.TryGetValue(key, out var existing) &&
            string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
            return false;

        persona.ConfigOverrides[key] = value;
        return true;
    }

    private static bool RemoveDuplicatePersonasByName(List<Persona> personas)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        for (var i = personas.Count - 1; i >= 0; i--)
        {
            var name = personas[i].Name ?? "";
            if (seen.Add(name)) continue;
            personas.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private static bool StripProfileControlledCliArgs(List<string> args)
    {
        if (args.Count == 0) return false;
        var skipNextFor = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--sandbox", "--ask-for-approval", "--model", "-m", "--profile", "-p", "--cd", "-C"
        };

        var changed = false;
        var kept = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var key = arg.Contains('=') ? arg[..arg.IndexOf('=')] : arg;
            if (skipNextFor.Contains(key))
            {
                changed = true;
                if (!arg.Contains('=') && i + 1 < args.Count) i++;
                continue;
            }

            if (string.Equals(key, "-c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "--config", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : (i + 1 < args.Count ? args[i + 1] : string.Empty);
                if (IsProfileControlledConfigOverride(value))
                {
                    changed = true;
                    if (!arg.Contains('=') && i + 1 < args.Count) i++;
                    continue;
                }
            }

            kept.Add(arg);
        }

        if (!changed) return false;
        args.Clear();
        args.AddRange(kept);
        return true;
    }

    private static bool IsProfileControlledConfigOverride(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var key = value.Split('=', 2)[0].Trim();
        return string.Equals(key, "profile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model_reasoning_effort", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "sandbox_mode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "approval_policy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "approvals_reviewer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model_instructions_file", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RenameConfigKey(Dictionary<string, string> dict, string oldKey, string newKey)
    {
        if (!dict.TryGetValue(oldKey, out var value)) return false;
        dict.Remove(oldKey);
        if (!dict.ContainsKey(newKey)) dict[newKey] = value;
        return true;
    }

    private static string NormalizeApprovalsReviewer(string? reviewer) =>
        string.Equals(reviewer?.Trim(), "auto_review", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reviewer?.Trim(), "guardian_subagent", StringComparison.OrdinalIgnoreCase)
            ? "auto_review"
            : "user";

    private static void AtomicWriteText(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private static void WithConfigLock(Action action)
    {
        var lockTaken = false;
        try
        {
            lockTaken = ConfigMutex.WaitOne(TimeSpan.FromSeconds(10));
            if (!lockTaken) throw new TimeoutException("Timed out waiting for configuration write lock.");
            action();
        }
        finally
        {
            if (lockTaken) ConfigMutex.ReleaseMutex();
        }
    }
}
