using System.Security.Cryptography;
using System.Text.Json;

namespace MonitorSwitcher;

internal sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _configPath;
    private AppConfig _current;

    public AppConfigStore()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonitorSwitcher");

        Directory.CreateDirectory(appDirectory);
        _configPath = Path.Combine(appDirectory, "config.json");
        _current = LoadOrCreate();
    }

    public AppConfig GetSnapshot()
    {
        lock (_gate)
        {
            return new AppConfig
            {
                Port = _current.Port,
                ApiToken = _current.ApiToken,
                LastTarget = _current.LastTarget
            };
        }
    }

    public void UpdateLastTarget(DisplayTarget target)
    {
        lock (_gate)
        {
            _current.LastTarget = target;
            SaveLocked();
        }
    }

    public DisplayTarget GetNextToggleTarget()
    {
        lock (_gate)
        {
            return _current.LastTarget == DisplayTarget.External
                ? DisplayTarget.Internal
                : DisplayTarget.External;
        }
    }

    private AppConfig LoadOrCreate()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config is not null && !string.IsNullOrWhiteSpace(config.ApiToken))
                {
                    return config;
                }
            }
            catch
            {
            }
        }

        var created = new AppConfig
        {
            ApiToken = CreateApiToken()
        };

        _current = created;
        SaveLocked();
        return created;
    }

    private void SaveLocked()
    {
        var json = JsonSerializer.Serialize(_current, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private static string CreateApiToken()
    {
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
