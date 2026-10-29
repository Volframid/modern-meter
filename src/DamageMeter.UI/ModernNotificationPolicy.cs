using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Data;

namespace DamageMeter.UI;

public static class ModernNotificationPolicy
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

    public static bool IsGameForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out var id);
        try
        {
            using var process = Process.GetProcessById((int)id);
            if (process.ProcessName.Equals("TERA", StringComparison.OrdinalIgnoreCase) ||
                process.ProcessName.Equals("TERA-Win64-Shipping", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        var className = new StringBuilder(128);
        GetClassName(window, className, className.Capacity);
        return className.ToString().Equals("LaunchUnrealUWindowsClient", StringComparison.Ordinal);
    }

    public static bool BackgroundOnly(EventType type) => type is EventType.AFK or EventType.Whisper or
        EventType.MatchingSuccess or EventType.ReadyCheck or EventType.OtherUserApply or EventType.Broker or
        EventType.PartyInvite or EventType.Trade or EventType.GenericContract or
        EventType.WakeUp or EventType.Mention;
}
