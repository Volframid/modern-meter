using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Data;
using Data.Actions.Notify.SoundElements;
using SpeechLib;

namespace DamageMeter.UI;

public static class ModernNotificationAudio
{
    public static CultureInfo SpeechCulture(string text, string configured)
    {
        if (text.Any(c => c >= '\u0400' && c <= '\u052f')) return CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            var culture = CultureInfo.GetCultureInfo(configured);
            return culture.IsNeutralCulture ? CultureInfo.CreateSpecificCulture(culture.Name) : culture;
        }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en-US"); }
    }

    // Language is required; gender and age are preferences, so they cannot
    // prevent an installed Russian voice from being used.
    public static bool Configure(SpVoiceClass voice, TextToSpeech speech)
    {
        var culture = SpeechCulture(speech.Text ?? "", speech.CultureInfo ?? "en-US");
        var choices = voice.GetVoices("Language=" + culture.LCID.ToString("X"), "Gender=" + speech.VoiceGender);
        try
        {
            if (choices.Count == 0) return false;
            var token = choices.Item(Math.Clamp(speech.VoicePosition, 0, choices.Count - 1));
            try { voice.Voice = token; }
            finally { Marshal.ReleaseComObject(token); }
            voice.Volume = Math.Clamp(speech.Volume, 0, 100);
            voice.Rate = Math.Clamp(speech.Rate, -10, 10);
            return true;
        }
        finally { Marshal.ReleaseComObject(choices); }
    }

    public static void Play(SoundInterface sound)
    {
        try
        {
            if (sound is not TextToSpeech speech) { sound.Play(); return; }
            var voice = new SpVoiceClass();
            try
            {
                if (!Configure(voice, speech)) { System.Media.SystemSounds.Exclamation.Play(); return; }
                voice.Speak(speech.Text, SpeechVoiceSpeakFlags.SVSFIsNotXML);
            }
            finally { Marshal.ReleaseComObject(voice); }
        }
        catch (Exception ex) { BasicTeraData.LogError("Notification audio: " + ex.GetType().Name, true, true); }
    }
}
