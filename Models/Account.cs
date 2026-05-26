using System;
using System.Text.Json.Serialization;

namespace CodexEnvironmentManager.Models;

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Type { get; set; } = "plus";
    public string? Provider { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool IsActive { get; set; }

    public string ResolvedProvider => string.IsNullOrWhiteSpace(Provider) ? "codex" : Provider;

    public string Icon => ResolvedProvider.ToLowerInvariant() switch
    {
        "kimi" => Type?.ToLowerInvariant() switch
        {
            "moonshot_api_key" => "🔑",
            _ => "🌙"
        },
        _ => Type?.ToLowerInvariant() switch
        {
            "api_key" => "🔑",
            _ => "🌐"
        }
    };

    public string Display => $"{Icon} {Name}";
}
