using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DamageMeter.UI.Broadcast;

public readonly record struct BroadcastBounds(double X, double Y, double Width, double Height);
public sealed record BroadcastLayer(string Id, BroadcastBounds Bounds, double Opacity, int Z, long Version, byte[] Png);

public sealed class BroadcastFrames
{
    private sealed record Frame(byte[] Json, IReadOnlyDictionary<string, byte[]> Images, int Count);
    private Frame _frame = new(Encoding.UTF8.GetBytes("{\"width\":1,\"height\":1,\"layers\":[]}"), new Dictionary<string, byte[]>(), 0);
    public int FramesPerSecond { get; set; } = 10;
    public byte[] Json => Volatile.Read(ref _frame).Json;
    public int Count => Volatile.Read(ref _frame).Count;
    public bool TryImage(string id, out byte[] bytes) => Volatile.Read(ref _frame).Images.TryGetValue(id, out bytes!);

    public void Publish(double width, double height, IEnumerable<BroadcastLayer> source)
    {
        var layers = source.ToArray();
        var images = layers.ToDictionary(layer => layer.Id, layer => layer.Png);
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            width = Math.Max(1, width), height = Math.Max(1, height),
            fps = Math.Clamp(FramesPerSecond, 5, 30),
            layers = layers.Select(layer => new
            {
                id = layer.Id, x = layer.Bounds.X, y = layer.Bounds.Y,
                width = layer.Bounds.Width, height = layer.Bounds.Height,
                opacity = Math.Clamp(layer.Opacity, 0, 1), z = layer.Z, version = layer.Version
            })
        });
        Interlocked.Exchange(ref _frame, new Frame(json, images, layers.Length));
    }

    // Other-display windows are fitted into the Meter display, preserving their local placement.
    public static BroadcastBounds Fold(BroadcastBounds window, BroadcastBounds monitor, BroadcastBounds canvas)
    {
        var scale = Math.Min(1, Math.Min(canvas.Width / Math.Max(1, window.Width), canvas.Height / Math.Max(1, window.Height)));
        var width = window.Width * scale; var height = window.Height * scale;
        return new BroadcastBounds(Math.Clamp(window.X - monitor.X, 0, Math.Max(0, canvas.Width - width)),
            Math.Clamp(window.Y - monitor.Y, 0, Math.Max(0, canvas.Height - height)), width, height);
    }
}
