using System;
using System.Text.Json.Serialization;

namespace CodexEnvironmentManager.Models;

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Type { get; set; } = "plus";
    public string? ApiKeyEncrypted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool IsActive { get; set; }

    public string Icon => Type == "api_key" ? "🔑" : "🌐";
    public string Display => $"{Icon} {Name}";
}
