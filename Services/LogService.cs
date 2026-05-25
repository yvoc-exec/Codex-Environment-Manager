using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexEnvironmentManager.Services;

public class LogService
{
    private readonly string _logDir;
    private readonly string _logFile;
    private readonly object _lock = new();

    public LogService()
    {
        _logDir = Path.Combine(JunctionManager.SwitcherDir, "logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, $"switcher_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string msg) => Write("INFO", msg);
    public void Warn(string msg) => Write("WARN", msg);
    public void Error(string msg, Exception? ex = null)
    {
        var sb = new StringBuilder(msg);
        if (ex != null) sb.Append($" | {ex.GetType().Name}: {ex.Message}");
        Write("ERROR", sb.ToString());
    }

    private void Write(string level, string msg)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {Redact(msg)}";
        lock (_lock)
        {
            File.AppendAllText(_logFile, line + Environment.NewLine);
        }
    }

    private static string Redact(string msg)
    {
        msg = Regex.Replace(msg, @"sk-[A-Za-z0-9_\-]{8,}", "sk-***REDACTED***");
        msg = Regex.Replace(msg, @"(OPENAI_API_KEY|CODEX_API_KEY|CODEX_ACCESS_TOKEN)\s*=\s*[^\s;]+", "$1=***REDACTED***", RegexOptions.IgnoreCase);
        msg = Regex.Replace(msg, @"Authorization\s*:\s*Bearer\s+[A-Za-z0-9_\.\-]+", "Authorization: Bearer ***REDACTED***", RegexOptions.IgnoreCase);
        msg = Regex.Replace(msg, @"(access_token|refresh_token|id_token)""?\s*[:=]\s*""?[^\s,;}""']+", "$1=***REDACTED***", RegexOptions.IgnoreCase);
        return msg;
    }

    public string GetLogPath() => _logFile;
}
