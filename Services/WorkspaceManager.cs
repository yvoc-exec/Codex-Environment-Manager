using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public class WorkspaceManager
{
    private readonly ConfigService _config;
    public WorkspaceManager(ConfigService config) => _config = config;
    public List<Workspace> GetWorkspaces() => _config.LoadList<Workspace>("workspaces");

    public void AddWorkspace(string name, string path, string? template = null)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {path}");
        var list = GetWorkspaces();
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (list.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A workspace named '{name}' already exists.");
        if (list.Any(w => string.Equals(Path.GetFullPath(w.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Workspace path is already registered: {path}");

        list.Add(new Workspace { Name = name, Path = normalizedPath, ProjectTemplate = template });
        _config.SaveList("workspaces", list);
    }

    public void DeleteWorkspace(string id)
    {
        var list = GetWorkspaces();
        list.RemoveAll(w => w.Id == id);
        _config.SaveList("workspaces", list);
    }

    public void UpdateLastSession(string workspaceId, string accountId, string personaId)
    {
        var list = GetWorkspaces();
        var ws = list.FirstOrDefault(w => w.Id == workspaceId);
        if (ws != null)
        {
            ws.LastAccountId = accountId;
            ws.LastPersonaId = personaId;
            _config.SaveList("workspaces", list);
        }
    }
}
