// Copyright (c) Shinra Meter contributors. MIT license.
#if !SERVER
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Tera;
using Tera.Game;

namespace DamageMeter.Sniffing;

public partial class ToolboxSniffer
{
    private readonly SemaphoreSlim _receiveGate = new SemaphoreSlim(1, 1);
    private IPAddress _dataAddress = IPAddress.Parse("127.0.0.60");
    private int _dataPort = 5301;
    private DateTime _lastTransportError = DateTime.MinValue;

    // Explicit endpoints allow isolated loopback transport tests; normal startup uses the original defaults.
    public ToolboxSniffer(string controlAddress, IPAddress dataAddress, int dataPort) : this()
    {
        ControlConnection = new ToolboxControlInterface(controlAddress);
        _dataAddress = dataAddress;
        _dataPort = dataPort;
    }

    private async Task ReceiveReliableAsync()
    {
        if (_failed) return;
        await _receiveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            while (Enabled)
            {
                TcpClient client = null;
                try
                {
                    var serverId = await ControlConnection.GetServerId().ConfigureAwait(false);
                    if (!Enabled) break;
                    if (serverId == 0) { await Task.Delay(250).ConfigureAwait(false); continue; }

                    await ControlConnection.AddHooks(MessageFactory.OpcodesList).ConfigureAwait(false);
                    var release = await ControlConnection.GetReleaseVersion().ConfigureAwait(false);
                    if (!Enabled) break;
                    if (release <= 0) throw new IOException("Toolbox did not return a release version.");

                    client = new TcpClient();
                    _dataConnection = client;
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        await client.ConnectAsync(_dataAddress, _dataPort, timeout.Token).ConfigureAwait(false);
                    if (!Enabled) break;

                    // Server lookup alone is not a live data connection. Publish it only after TCP connects.
                    OnNewConnection(BasicTeraData.Instance.Servers.GetServer(serverId));
                    ReleaseVersionUpdated?.Invoke(release);
                    Connected = true;
                    TransportLog("connected", false);
                    var stream = client.GetStream();
                    var header = new byte[2];
                    while (Enabled && Connected)
                    {
                        await ReadFramePart(stream, header, 2).ConfigureAwait(false);
                        var length = BitConverter.ToUInt16(header, 0);
                        if (length < 4) throw new IOException("Invalid Toolbox frame length: " + length);
                        var payload = new byte[length - 2];
                        await ReadFramePart(stream, payload, payload.Length).ConfigureAwait(false);
                        OnMessageReceived(new Message(DateTime.UtcNow, MessageDirection.Unspecified, new ArraySegment<byte>(payload)));
                    }
                }
                catch (Exception error)
                {
                    if (Enabled) TransportLog(error.GetType().Name + ": " + error.Message, true);
                }
                finally
                {
                    client?.Dispose();
                    try { Connected = false; }
                    catch (Exception error) { TransportLog("Disconnect callback: " + error.Message, true); }
                }
                if (Enabled) await Task.Delay(500).ConfigureAwait(false);
            }
        }
        finally { _receiveGate.Release(); }
    }

    private static async Task ReadFramePart(NetworkStream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Toolbox data connection closed.");
            offset += read;
        }
    }

    private void TransportLog(string text, bool error)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (error && now - _lastTransportError < TimeSpan.FromSeconds(10)) return;
            if (error) _lastTransportError = now;
            var folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "transport.log"), now.ToString("O") + " " + text + Environment.NewLine);
        }
        catch { /* Diagnostics must never stop the receiver; no remote reporting. */ }
    }
}
#endif
