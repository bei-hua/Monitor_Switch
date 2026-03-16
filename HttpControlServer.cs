using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MonitorSwitcher;

internal sealed class HttpControlServer : IDisposable
{
    private readonly AppConfigStore _configStore;
    private readonly Func<DisplayTarget, bool> _switchAction;
    private readonly Func<string> _statusTextFactory;
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private Task? _acceptLoopTask;

    public HttpControlServer(
        AppConfigStore configStore,
        Func<DisplayTarget, bool> switchAction,
        Func<string> statusTextFactory)
    {
        _configStore = configStore;
        _switchAction = switchAction;
        _statusTextFactory = statusTextFactory;
    }

    public void Start()
    {
        var config = _configStore.GetSnapshot();
        _listener = new TcpListener(IPAddress.Any, config.Port);
        _listener.Start();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
    }

    public void Dispose()
    {
        _shutdown.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        try
        {
            _acceptLoopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;

            string? requestLine;
            try
            {
                requestLine = await reader.ReadLineAsync().WaitAsync(cancellationToken);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            while (true)
            {
                string? headerLine;
                try
                {
                    headerLine = await reader.ReadLineAsync().WaitAsync(cancellationToken);
                }
                catch
                {
                    return;
                }

                if (string.IsNullOrEmpty(headerLine))
                {
                    break;
                }
            }

            var response = BuildResponse(requestLine);
            await WriteResponseAsync(stream, response, cancellationToken);
        }
    }

    private Response BuildResponse(string requestLine)
    {
        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return Response.PlainText(400, "Bad Request", "Invalid HTTP request.");
        }

        var method = parts[0].ToUpperInvariant();
        var rawTarget = parts[1];
        if (!Uri.TryCreate($"http://localhost{rawTarget}", UriKind.Absolute, out var uri))
        {
            return Response.PlainText(400, "Bad Request", "Invalid URL.");
        }

        if (method != "GET")
        {
            return Response.PlainText(405, "Method Not Allowed", "Only GET is supported.");
        }

        var config = _configStore.GetSnapshot();
        var query = ParseQueryString(uri.Query);
        if (!query.TryGetValue("token", out var token) || !string.Equals(token, config.ApiToken, StringComparison.Ordinal))
        {
            return Response.Json(401, "Unauthorized", new
            {
                ok = false,
                error = "Invalid token."
            });
        }

        return uri.AbsolutePath switch
        {
            "/" => BuildHomeResponse(config),
            "/api/status" => Response.Json(200, "OK", new
            {
                ok = true,
                status = _statusTextFactory(),
                lastTarget = config.LastTarget.ToString().ToLowerInvariant(),
                addresses = NetworkAddressProvider.GetLanAddresses().Select(ip => $"http://{ip}:{config.Port}/").ToArray()
            }),
            "/api/switch/internal" => BuildSwitchResponse(DisplayTarget.Internal),
            "/api/switch/external" => BuildSwitchResponse(DisplayTarget.External),
            "/api/switch/toggle" => BuildSwitchResponse(_configStore.GetNextToggleTarget()),
            _ => Response.Json(404, "Not Found", new
            {
                ok = false,
                error = "Unknown path."
            })
        };
    }

    private Response BuildHomeResponse(AppConfig config)
    {
        var urls = NetworkAddressProvider.GetLanAddresses()
            .Select(ip => $"http://{ip}:{config.Port}")
            .ToArray();

        var lines = new List<string>
        {
            "Monitor Switcher is running.",
            string.Empty,
            $"Status: {_statusTextFactory()}",
            $"Token: {config.ApiToken}",
            string.Empty,
            "Available URLs:"
        };

        lines.AddRange(urls.Select(url => $"{url}/api/status?token={config.ApiToken}"));
        lines.Add(string.Empty);
        lines.Add("Switch examples:");
        lines.AddRange(urls.Select(url => $"{url}/api/switch/internal?token={config.ApiToken}"));
        lines.AddRange(urls.Select(url => $"{url}/api/switch/external?token={config.ApiToken}"));
        lines.AddRange(urls.Select(url => $"{url}/api/switch/toggle?token={config.ApiToken}"));

        return Response.PlainText(200, "OK", string.Join(Environment.NewLine, lines));
    }

    private Response BuildSwitchResponse(DisplayTarget target)
    {
        var switched = _switchAction(target);
        if (!switched)
        {
            return Response.Json(500, "Internal Server Error", new
            {
                ok = false,
                error = "Display switch failed."
            });
        }

        return Response.Json(200, "OK", new
        {
            ok = true,
            target = target.ToString().ToLowerInvariant()
        });
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0]);
            var value = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, Response response, CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        var headers = new StringBuilder()
            .Append("HTTP/1.1 ")
            .Append(response.StatusCode)
            .Append(' ')
            .Append(response.StatusText)
            .Append("\r\n")
            .Append("Content-Type: ")
            .Append(response.ContentType)
            .Append("; charset=utf-8\r\n")
            .Append("Content-Length: ")
            .Append(bodyBytes.Length)
            .Append("\r\n")
            .Append("Connection: close\r\n")
            .Append("\r\n")
            .ToString();

        var headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private readonly record struct Response(
        int StatusCode,
        string StatusText,
        string ContentType,
        string Body)
    {
        public static Response PlainText(int statusCode, string statusText, string body) =>
            new(statusCode, statusText, "text/plain", body);

        public static Response Json(int statusCode, string statusText, object payload) =>
            new(statusCode, statusText, "application/json", JsonSerializer.Serialize(payload));
    }
}
