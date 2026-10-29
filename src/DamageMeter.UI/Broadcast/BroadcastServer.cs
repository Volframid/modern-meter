using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DamageMeter.UI.Broadcast;

/// <summary>Read-only loopback HTTP source; no account API, file serving or remote listener.</summary>
public sealed class BroadcastServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly BroadcastFrames _frames;
    private readonly string _prefix;
    private readonly byte[] _html;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _clients = new(16);
    private long _lastViewer;
    public int Port { get; }
    public string Url => $"http://127.0.0.1:{Port}{_prefix}";
    public bool HasViewer => DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastViewer) < TimeSpan.FromSeconds(3).Ticks;

    public BroadcastServer(int port, string token, BroadcastFrames frames, string? html = null)
    {
        if (token.Length != 32 || !token.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid local source key", nameof(token));
        _prefix = "/" + token + "/"; _frames = frames;
        if (html == null)
        {
            using var resource = typeof(BroadcastServer).Assembly.GetManifestResourceStream("ModernMeter.BroadcastOverlay.html")
                ?? throw new InvalidOperationException("Broadcast page missing");
            using var reader = new StreamReader(resource); html = reader.ReadToEnd();
        }
        _html = Encoding.UTF8.GetBytes(html);
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                if (_stop.IsCancellationRequested) { client.Dispose(); break; }
                if (!await _clients.WaitAsync(0).ConfigureAwait(false)) { client.Dispose(); continue; }
                _ = ServeAsync(client);
            }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var stream = client.GetStream(); var buffer = new byte[8192]; var count = 0;
                while (count < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(count), timeout.Token).ConfigureAwait(false);
                    if (read == 0) return; count += read;
                    if (Encoding.ASCII.GetString(buffer, 0, count).Contains("\r\n\r\n")) break;
                }
                var request = Encoding.ASCII.GetString(buffer, 0, count).Split("\r\n");
                var first = request[0].Split(' ');
                var code = 404; var kind = "text/plain; charset=utf-8"; var body = Array.Empty<byte>();
                if (first.Length != 3 || !Encoding.ASCII.GetString(buffer, 0, count).Contains("\r\n\r\n") || !request.Any(line => line.Equals($"Host: 127.0.0.1:{Port}", StringComparison.OrdinalIgnoreCase)
                    || line.Equals($"Host: localhost:{Port}", StringComparison.OrdinalIgnoreCase))) code = 400;
                else if (first[0] != "GET") code = 405;
                else
                {
                    var path = first[1].Split('?')[0];
                    if (path == _prefix) { code = 200; kind = "text/html; charset=utf-8"; body = _html; Touch(); }
                    else if (path == _prefix + "state") { code = 200; kind = "application/json; charset=utf-8"; body = _frames.Json; Touch(); }
                    else if (path.StartsWith(_prefix + "frame/", StringComparison.Ordinal))
                    {
                        var id = path.Substring((_prefix + "frame/").Length);
                        if (id.Length is > 0 and <= 64 && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_') && _frames.TryImage(id, out var png))
                        { code = 200; kind = "image/png"; body = png; Touch(); }
                    }
                }
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {(code == 200 ? "OK" : "Unavailable")}\r\nContent-Type: {kind}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nCross-Origin-Resource-Policy: same-origin\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, timeout.Token).ConfigureAwait(false);
                if (body.Length > 0) await stream.WriteAsync(body, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        finally { _clients.Release(); }
    }

    private void Touch() => Interlocked.Exchange(ref _lastViewer, DateTime.UtcNow.Ticks);
    public void Dispose() { _stop.Cancel(); _listener.Stop(); }
}
