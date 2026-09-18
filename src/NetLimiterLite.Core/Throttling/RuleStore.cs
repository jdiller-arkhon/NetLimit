using System.Text.Json;
using NetLimiterLite.Core.Model;

namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Loads/saves the user's bandwidth rules as JSON, defaulting to
/// %APPDATA%/NetLimiterLite/rules.json on Windows.
/// </summary>
public sealed class RuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath { get; }

    public RuleStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    private static string DefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "NetLimiterLite", "rules.json");
    }

    public List<BandwidthRule> Load()
    {
        if (!File.Exists(FilePath)) return new List<BandwidthRule>();

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<BandwidthRule>>(json, JsonOptions) ?? new List<BandwidthRule>();
    }

    public void Save(IEnumerable<BandwidthRule> rules)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(rules.ToList(), JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
