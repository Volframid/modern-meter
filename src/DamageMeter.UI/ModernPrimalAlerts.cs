using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using DamageMeter;
using Data;
using Data.Events.Abnormality;

namespace DamageMeter.UI;

/// <summary>Extend existing 2.0 mount rules without rewriting the user's event configuration.</summary>
public static class ModernPrimalAlerts
{
    // Installed Asura client data: Primal Omen, Primal Mark and Queen's Wrath.
    public static IReadOnlyList<int> PrimalIds { get; } = new[] { 6097, 6098, 6101 };
    private static readonly HashSet<int> TwoTimesMountIds = new()
        { 6002, 6004, 6013, 6018, 6026, 6041, 6059, 6069, 6071, 6087, 6089, 6096 };
    private static readonly HashSet<int> ObservedMountIds = new();
    private static readonly object ObservationLock = new();
    private static volatile bool _asura;
    public static bool IsAsura => _asura;

    // Register independently of window construction, before transport can connect.
    public static void Initialize(PacketProcessor processor)
    {
        ModernNotificationContext.Initialize();
        processor.Connected -= OnConnected;
        processor.Connected += OnConnected;
        if (processor.Server != null) OnConnected(processor.Server.Name);
    }

    private static void OnConnected(string servername)
    {
        _asura = servername.Contains("Asura", StringComparison.OrdinalIgnoreCase);
        ModernMountNotifications.Reset();
        if (!_asura) return;
        lock (ObservationLock) ObservedMountIds.Clear();
        var rules = BasicTeraData.Instance.EventsData.EventsCommon.Keys.OfType<AbnormalityEvent>().ToArray();
        var added = Extend(rules);
        var matching = rules.Where(r => r.Target == AbnormalityTargetType.Self && r.Trigger == AbnormalityTriggerType.Added
            && PrimalIds.All(r.Ids.ContainsKey)).ToArray();
        Trace($"Asura rules prepared: aliasesAdded={added}, rules={matching.Length}, active={matching.Count(r => r.Active)}");
    }

    // Read only the meter's existing snapshot; no additional packet hooks/capture.
    public static void Observe(UiUpdateMessage message)
    {
        ModernMountNotifications.Recover(message);
        if (!_asura) return;
        var player = PacketProcessor.Instance.PlayerTracker?.Me();
        if (player == null || message.Abnormals == null) return;
        foreach (var dot in message.Abnormals.Get(player).Times.Keys)
        {
            if (dot.Id < 6000 || dot.Id > 6120) continue;
            lock (ObservationLock) { if (!ObservedMountIds.Add(dot.Id)) continue; }
            var rules = BasicTeraData.Instance.EventsData.AddedRemovedAbnormalities.Keys.Count(r => r.Ids.ContainsKey(dot.Id));
            var flashes = message.Flash.Count(f => f.Balloon?.Icon == dot.EffectIcon);
            Trace($"Own mount effect: id={dot.Id}, matchingLoadedRules={rules}, flashInSnapshot={flashes}");
        }
    }

    internal static void Trace(string text)
    {
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "alerts.log"), DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static int Extend(IEnumerable<AbnormalityEvent> rules)
    {
        var added = 0;
        foreach (var rule in rules)
        {
            if (rule.Target != AbnormalityTargetType.Self || rule.Trigger != AbnormalityTriggerType.Added
                || rule.Ids == null || !rule.Ids.Keys.Any(TwoTimesMountIds.Contains)) continue;
            foreach (var id in PrimalIds)
                if (rule.Ids.TryAdd(id, 0)) added++;
        }
        return added;
    }
}
