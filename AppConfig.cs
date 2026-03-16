using System.Text.Json.Serialization;

namespace MonitorSwitcher;

internal sealed class AppConfig
{
    public int Port { get; set; } = 8765;

    public string ApiToken { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DisplayTarget LastTarget { get; set; } = DisplayTarget.External;
}
