using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DamageMeter.UI.Broadcast;

public sealed class BroadcastPreferences
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 17195;
    public int FramesPerSecond { get; set; } = 10;
    public string Token { get; set; } = "";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public static BroadcastPreferences Load(string path)
    {
        BroadcastPreferences result;
        try { result = File.Exists(path) ? JsonSerializer.Deserialize<BroadcastPreferences>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { result = new(); }
        result.Port = result.Port is >= 1024 and <= 65535 ? result.Port : 17195;
        result.FramesPerSecond = Math.Clamp(result.FramesPerSecond, 5, 30);
        if (string.IsNullOrEmpty(result.Token) || result.Token.Length != 32 || !System.Linq.Enumerable.All(result.Token, Uri.IsHexDigit))
        {
            result.Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            try { result.Save(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { result.Enabled = false; }
        }
        return result;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
