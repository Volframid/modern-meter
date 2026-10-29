using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using DamageMeter;
using Data;
using Data.Actions.Notify;
using Data.Actions.Notify.SoundElements;
using Data.Events.Abnormality;
using Tera.Game;

namespace DamageMeter.UI;

// Exact native metadata also covers sound-only actions; never classify by icon/text.
internal static class ModernNotificationContext
{
    internal sealed class Effect
    {
        public EntityId Target;
        public int Id;
        public AbnormalityTriggerType Trigger;
        public bool OwnMount;
        public long Ticket;
    }
    private static readonly ConditionalWeakTable<NotifyFlashMessage, Effect> Effects = new();
    internal static Effect? Get(NotifyFlashMessage flash) => Effects.TryGetValue(flash, out var effect) ? effect : null;

    public static void Initialize()
    {
        var type = typeof(PacketProcessor).Assembly.GetType("DamageMeter.Processing.NotifyProcessor", true)!;
        var abnormality = type.GetField("ModernAbnormalityObserved", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(type.FullName, "ModernAbnormalityObserved");
        var credits = type.GetField("ModernCreditsObserved", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(type.FullName, "ModernCreditsObserved");
        abnormality.SetValue(null, (Action<object, object, int, int>)Record);
        credits.SetValue(null, (Action<object, int>)UpdateCredits);
    }

    private static void Record(object value, object target, int id, int trigger)
    {
        if (value is not NotifyFlashMessage flash || target is not EntityId entity) return;
        var me = PacketProcessor.Instance.EntityTracker?.MeterUser;
        Effects.Add(flash, new Effect { Target = entity, Id = id, Trigger = (AbnormalityTriggerType)trigger,
            OwnMount = me != null && me.Id == entity && ModernMountNotifications.Category(id) != 0 });
        ModernMountNotifications.RefreshEffective();
    }

    private static void UpdateCredits(object value, int amount)
    {
        if (value is not NotifyFlashMessage flash || flash.Sound is not TextToSpeech speech) return;
        var processor = PacketProcessor.Instance;
        var name = processor.Server?.Name;
        var factory = typeof(PacketProcessor).GetProperty("MessageFactory", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(processor);
        var version = Convert.ToInt64(factory?.GetType().GetField("Version")?.GetValue(factory) ?? 0);
        var cap = version == 351828 ? 9000
            : name == "Menma's TERA" || name == "MT: The Dream" ? 100000 : 20000;
        // Keep voice settings, custom file sounds and the existing popup intact.
        speech.Text = amount >= cap ? ModernLocalization.Get("Vanguard credits are maxed out")
            : ModernLocalization.Get("Vanguard credits are close to cap");
    }
}
