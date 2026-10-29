using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using DamageMeter;
using Data;
using Data.Actions.Notify;
using Data.Events.Abnormality;
using Tera.Game;
using Tera.Game.Abnormality;

namespace DamageMeter.UI;

/// <summary>Announce only the effective, currently active mount effect on the meter user.</summary>
public static class ModernMountNotifications
{
    public const string BlueCloud = "icon_skills.oriental_wind_blue_tex";
    public const string OrangeCloud = "icon_skills.oriental_wind_red_tex";
    private static readonly int[] HalfIds = { 6001, 6003, 6012, 6017, 6025, 6040, 6058, 6068, 6070, 6086, 6088 };
    private static readonly int[] DoubleIds = { 6002, 6004, 6013, 6018, 6026, 6041, 6059, 6069, 6071, 6087, 6089, 6096 };
    private static readonly object Gate = new();
    private static readonly Lazy<Action<EntityId, int, AbnormalityTriggerType, int>> Replay = new(CreateReplay);
    private static readonly FieldInfo? TrackerField = typeof(PacketProcessor).GetField("AbnormalityTracker",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static Activation? _effective;
    private static long _nextTicket;

    private sealed class Activation
    {
        public Abnormality Buff = null!;
        public EntityId Owner;
        public long Ticket, Expiry, RetryUntil;
        public bool Delivered;
    }

    internal static int Category(int id) => DoubleIds.Contains(id)
        || (ModernPrimalAlerts.IsAsura && ModernPrimalAlerts.PrimalIds.Contains(id)) ? 2 : HalfIds.Contains(id) ? 1 : 0;

    public static void Reset() { lock (Gate) { Volatile.Write(ref _effective, null); _nextTicket++; } }

    // Only the analysis thread enumerates the mutable tracker. UI/audio read a ticket.
    internal static void RefreshEffective()
    {
        lock (Gate)
        {
            var data = BasicTeraData.Instance.WindowData;
            var processor = PacketProcessor.Instance;
            var me = processor.EntityTracker?.MeterUser;
            var tracker = TrackerField?.GetValue(processor) as AbnormalityTracker;
            var buff = data.EnableChat && !data.UserPaused && me != null && tracker != null
                ? tracker.BuffList(me.Id).Where(ab => Category(ab.HotDot.Id) != 0 && ab.Duration > 0 && ab.TimeBeforeEnd > 0)
                    .OrderByDescending(ab => Category(ab.HotDot.Id)).ThenByDescending(ab => ab.FirstHit).FirstOrDefault()
                : null;
            var previous = _effective;
            if (buff == null) { Volatile.Write(ref _effective, null); return; }
            var expiry = buff.FirstHit + buff.Duration * TimeSpan.TicksPerMillisecond;
            if (previous != null && previous.Owner == me!.Id && ReferenceEquals(previous.Buff, buff) && buff.FirstHit < previous.Expiry)
            { Volatile.Write(ref previous.Expiry, expiry); return; }
            // A still-active 1.5 can become effective after 2.0 ends. Other
            // recoveries must be within 2.5 s of the actual activation.
            var start = previous != null && Category(previous.Buff.HotDot.Id) == 2 && Category(buff.HotDot.Id) == 1
                ? DateTime.UtcNow.Ticks : buff.FirstHit;
            Volatile.Write(ref _effective, new Activation { Buff = buff, Owner = me!.Id, Ticket = ++_nextTicket,
                Expiry = expiry, RetryUntil = Math.Min(expiry, start + 2500 * TimeSpan.TicksPerMillisecond) });
        }
    }

    public static int Weight(NotifyFlashMessage flash) => ModernNotificationContext.Get(flash) is { OwnMount: true } effect ? Category(effect.Id) : 0;
    public static void NormalizeIcon(NotifyFlashMessage flash)
    {
        var weight = Weight(flash);
        if (weight != 0 && flash.Balloon != null) flash.Balloon.Icon = weight == 2 ? OrangeCloud : BlueCloud;
    }

    public static bool CanDeliver(NotifyFlashMessage flash)
    {
        var effect = ModernNotificationContext.Get(flash);
        if (effect is not { OwnMount: true }) return true;
        var active = Volatile.Read(ref _effective);
        var data = BasicTeraData.Instance.WindowData;
        return data.EnableChat && !data.UserPaused && active != null && effect.Ticket != 0 && effect.Ticket == active.Ticket
            && effect.Id == active.Buff.HotDot.Id && effect.Target == active.Owner && DateTime.UtcNow.Ticks < Volatile.Read(ref active.Expiry);
    }

    public static void Recover(UiUpdateMessage message)
    {
        RefreshEffective();
        lock (Gate)
        {
            var processor = PacketProcessor.Instance;
            var candidates = message.Flash.Concat(processor.FlashMessage)
                .Where(f => ModernNotificationContext.Get(f) is { OwnMount: true }).ToArray();
            message.Flash.RemoveAll(f => ModernNotificationContext.Get(f) is { OwnMount: true });
            processor.FlashMessage.RemoveAll(f => ModernNotificationContext.Get(f) is { OwnMount: true });
            var active = _effective;
            if (active == null || active.Delivered) return;
            bool Matches(NotifyFlashMessage flash) => ModernNotificationContext.Get(flash) is { } effect
                && effect.Id == active.Buff.HotDot.Id && effect.Target == active.Owner && effect.Trigger == AbnormalityTriggerType.Added;
            var accepted = candidates.Where(Matches).Distinct().ToArray();
            var recovered = false;
            if (accepted.Length == 0 && DateTime.UtcNow.Ticks <= active.RetryUntil)
            {
                var output = processor.FlashMessage; var start = output.Count;
                try
                {
                    // Native rules still evaluate enabled state, boss, class,
                    // blacklist, foreground, stack and death conditions.
                    Replay.Value(active.Owner, active.Buff.HotDot.Id, AbnormalityTriggerType.Added, active.Buff.Stack);
                    accepted = output.Skip(start).Where(Matches).ToArray();
                    recovered = accepted.Length != 0;
                }
                catch (Exception error)
                { active.Delivered = true; ModernPrimalAlerts.Trace("Mount recovery unavailable: " + error.GetType().Name); }
                finally { if (output.Count > start) output.RemoveRange(start, output.Count - start); }
            }
            if (accepted.Length == 0) return;
            foreach (var flash in accepted)
            {
                ModernNotificationContext.Get(flash)!.Ticket = active.Ticket;
                NormalizeIcon(flash); message.Flash.Add(flash);
            }
            active.Delivered = true;
            ModernPrimalAlerts.Trace($"Effective mount buff: id={active.Buff.HotDot.Id}, category={Category(active.Buff.HotDot.Id)}, recovered={recovered}, actions={accepted.Length}");
        }
    }

    private static Action<EntityId, int, AbnormalityTriggerType, int> CreateReplay()
    {
        var type = typeof(PacketProcessor).Assembly.GetType("DamageMeter.Processing.NotifyProcessor", true)!;
        var instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var method = type.GetMethod("AbnormalityNotifierCommon", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName, "AbnormalityNotifierCommon");
        return (Action<EntityId, int, AbnormalityTriggerType, int>)method.CreateDelegate(typeof(Action<EntityId, int, AbnormalityTriggerType, int>), instance);
    }
}
