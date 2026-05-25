using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CodexEnvironmentManager.Services;

public static class SnapshotExporter
{
    public static void ExportAccountSnapshot(string accountPath, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Codex Account Snapshot");
        sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Account path: `{accountPath}`");
        sb.AppendLine();

        var orbitPath = Path.Combine(accountPath, "orbit.db");
        if (File.Exists(orbitPath))
            AppendOrbitDbSnapshot(orbitPath, sb);
        else
            sb.AppendLine("*(No orbit.db found)*");

        sb.AppendLine();
        var historyPath = Path.Combine(accountPath, "history.jsonl");
        if (File.Exists(historyPath))
            AppendHistoryJsonl(historyPath, sb);
        else
            sb.AppendLine("*(No history.jsonl found)*");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportToMarkdown(string orbitDbPath, string outputPath)
    {
        var accountPath = Path.GetDirectoryName(orbitDbPath) ?? ".";
        ExportAccountSnapshot(accountPath, outputPath);
    }

    private static void AppendOrbitDbSnapshot(string orbitDbPath, StringBuilder sb)
    {
        sb.AppendLine("## orbit.db");
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = orbitDbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            };
            using var conn = new SqliteConnection(builder.ToString());
            conn.Open();

            var tables = GetTables(conn);
            sb.AppendLine("Tables: " + string.Join(", ", tables.Select(t => $"`{t}`")));
            sb.AppendLine();

            if (tables.Contains("threads"))
                AppendThreads(conn, sb);
            else
                sb.AppendLine("*(threads table not found — schema may differ)*");

            if (tables.Contains("messages"))
                AppendMessages(conn, sb);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"*(Error reading orbit.db: {ex.Message})*");
        }
    }

    private static List<string> GetTables(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read()) tables.Add(reader.GetString(0));
        return tables;
    }

    private static void AppendThreads(SqliteConnection conn, StringBuilder sb)
    {
        var columns = GetColumns(conn, "threads");
        var wanted = new[] { "id", "title", "created_at", "updated_at" }.Where(columns.Contains).ToList();
        if (wanted.Count == 0) return;

        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {string.Join(", ", wanted.Select(QuoteIdentifier))} FROM threads LIMIT 100";
        using var tr = cmd.ExecuteReader();
        sb.AppendLine("### Threads");
        while (tr.Read())
        {
            for (var i = 0; i < wanted.Count; i++)
                sb.AppendLine($"- {wanted[i]}: `{SafeValue(tr, i)}`");
            sb.AppendLine();
        }
    }

    private static void AppendMessages(SqliteConnection conn, StringBuilder sb)
    {
        var columns = GetColumns(conn, "messages");
        var wanted = new[] { "thread_id", "role", "content", "created_at" }.Where(columns.Contains).ToList();
        if (wanted.Count == 0) return;

        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {string.Join(", ", wanted.Select(QuoteIdentifier))} FROM messages LIMIT 100";
        using var tr = cmd.ExecuteReader();
        sb.AppendLine("### Messages");
        while (tr.Read())
        {
            sb.AppendLine("---");
            for (var i = 0; i < wanted.Count; i++)
            {
                var value = SafeValue(tr, i);
                if (wanted[i].Equals("content", StringComparison.OrdinalIgnoreCase) && value.Length > 4000)
                    value = value[..4000] + "\n...(truncated)...";
                sb.AppendLine($"**{wanted[i]}:** {value}");
            }
        }
    }

    private static List<string> GetColumns(SqliteConnection conn, string table)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)})";
        using var reader = cmd.ExecuteReader();
        var cols = new List<string>();
        while (reader.Read()) cols.Add(reader.GetString(1));
        return cols;
    }

    private static void AppendHistoryJsonl(string historyPath, StringBuilder sb)
    {
        sb.AppendLine("## history.jsonl");
        try
        {
            var lines = File.ReadLines(historyPath).TakeLastSafe(100).ToList();
            if (lines.Count == 0)
            {
                sb.AppendLine("*(history.jsonl is empty)*");
                return;
            }

            sb.AppendLine($"Showing last {lines.Count} line(s).");
            sb.AppendLine();
            sb.AppendLine("```jsonl");
            foreach (var line in lines)
                sb.AppendLine(line.Length > 4000 ? line[..4000] + " ...(truncated)" : line);
            sb.AppendLine("```");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"*(Error reading history.jsonl: {ex.Message})*");
        }
    }

    private static string SafeValue(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index)) ?? "";

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";
}

internal static class EnumerableExtensions
{
    public static IEnumerable<T> TakeLastSafe<T>(this IEnumerable<T> source, int count)
    {
        var queue = new Queue<T>();
        foreach (var item in source)
        {
            queue.Enqueue(item);
            if (queue.Count > count) queue.Dequeue();
        }
        return queue;
    }
}
