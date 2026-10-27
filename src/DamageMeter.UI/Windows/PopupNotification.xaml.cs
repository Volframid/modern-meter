using System.Threading.Tasks;
using System;
using System.Windows;
using Data;
using Data.Actions.Notify;
using System.Linq;
using System.Collections.Generic;
using Data.Actions.Notify.SoundElements;
using System.Windows.Threading;
using DamageMeter.UI.Windows;

namespace DamageMeter.UI
{
    /// <summary>
    ///     Logique d'interaction pour PopupNotification.xaml
    /// </summary>
    public partial class PopupNotification
    {
        private readonly Action _present;
        private readonly Func<bool> _gameForeground;
        private readonly Action<SoundInterface> _playSound;
        private readonly DispatcherTimer _focusTimer;

        public double NotificationBackgroundOpacity
        {
            get => (double)GetValue(NotificationBackgroundOpacityProperty);
            private set => SetValue(NotificationBackgroundOpacityProperty, value);
        }
        public static readonly DependencyProperty NotificationBackgroundOpacityProperty = DependencyProperty.Register(
            nameof(NotificationBackgroundOpacity), typeof(double), typeof(PopupNotification), new PropertyMetadata(1d));

        public PopupNotification() : this(null) { }

        // Allows queue validation without displaying a native window or taking focus.
        public PopupNotification(Action? present, Func<bool>? gameForeground = null, Action<SoundInterface>? playSound = null)
        {
            _present = present ?? PresentNotification;
            _gameForeground = gameForeground ?? ModernNotificationPolicy.IsGameForeground;
            _playSound = playSound ?? ModernNotificationAudio.Play;
            _focusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
                (_, _) => RemoveFocusedNotifications(), Dispatcher);
            _focusTimer.Stop();
            InitializeComponent();
            NotificationBackgroundOpacity = ModernPreferences.GetNotificationOpacity(BasicTeraData.Instance.WindowData);
            SettingsWindowViewModel.NotificationOpacityChanged += OnNotificationOpacityChanged;
            notificationBalloons = new SynchronizedObservableCollection<Balloon>();
            notificationBalloons.CollectionChanged += NotificationBalloons_CollectionChanged;
            content.ItemsSource = notificationBalloons;

            this.LastSnappedPoint = BasicTeraData.Instance.WindowData.PopupNotificationLocation;
            this.Left = this.LastSnappedPoint?.X ?? 0;
            this.Top = this.LastSnappedPoint?.Y ?? 0;

        }

        private void OnNotificationOpacityChanged(double value)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.Invoke(() => NotificationBackgroundOpacity = value);
        }

        protected override void OnClosed(EventArgs e)
        {
            SettingsWindowViewModel.NotificationOpacityChanged -= OnNotificationOpacityChanged;
            _focusTimer.Stop();
            base.OnClosed(e);
        }

        private void PresentNotification()
        {
            Topmost = false;
            Topmost = true;
            if (!IsVisible) ShowWindow();
        }

        private static PopupNotification _instance = null;
        public static PopupNotification Instance => _instance ?? (_instance = new PopupNotification());


        private void NotificationBalloons_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(notificationBalloons.Count == 0)
            {
                _focusTimer.Stop();
                HideWindow();
            }
        }

        public void RemoveFocusedNotifications()
        {
            if (!_gameForeground()) return;
            foreach (var balloon in notificationBalloons.ToSyncArray().Where(b => ModernNotificationPolicy.BackgroundOnly(b.EventType)))
                notificationBalloons.Remove(balloon);
        }

        public SynchronizedObservableCollection<Balloon> notificationBalloons;

        public void AddNotification(List<NotifyFlashMessage> flashList)
        {
            foreach(var flash in flashList.OrderByDescending(x => x.Priority))
            {
                AddNotification(flash);
            }
        }


        public void AddNotification(NotifyFlashMessage flash)
        {
            Dispatcher.Invoke(() =>
            {

                if (flash == null) { return; }
                var backgroundOnly = flash.Balloon != null && ModernNotificationPolicy.BackgroundOnly(flash.Balloon.EventType);
                if (backgroundOnly && _gameForeground()) return;

                if (flash.Balloon != null && flash.Balloon.DisplayTime >= 500)
                {
                    var existing = notificationBalloons.ToSyncArray().FirstOrDefault(x => x.TitleText == flash.Balloon.TitleText && x.Icon == flash.Balloon.Icon);
                    if (existing == null)
                    {
                        if (!SnappedToBottom)
                        {
                            notificationBalloons.Add(flash.Balloon);
                        }
                        else
                        {
                            notificationBalloons.Insert(0, flash.Balloon);
                        }
                    }
                    else
                    {
                        if (flash.Balloon.EventType == EventType.Whisper)
                        {
                            if (!SnappedToBottom)
                            {
                                notificationBalloons.Add(flash.Balloon);
                            }
                            else
                            {
                                notificationBalloons.Insert(0, flash.Balloon);
                            }
                        }
                        else
                        {
                            existing.UpdateBody(flash.Balloon.BodyText);
                            existing.Refresh();
                        }
                    }

                    _present();
                    _focusTimer.Start();
                }

                if (!BasicTeraData.Instance.WindowData.MuteSound && flash.Sound != null)
                    Task.Run(() => { if (!backgroundOnly || !_gameForeground()) _playSound(flash.Sound); });
            });
        }

        public double DHeight
        {
            get => (double)GetValue(DHeightProperty);
            set => SetValue(DHeightProperty, value);
        }

        public static readonly DependencyProperty DHeightProperty = DependencyProperty.Register("DHeight", typeof(double), typeof(PopupNotification));

        internal void RemoveMe(Balloon context)
        {
            var item = notificationBalloons.ToSyncArray().FirstOrDefault(x => x.TitleText == context.TitleText && x.Icon == context.Icon && x.BodyText == context.BodyText);
            if (item != null) notificationBalloons.Remove(item);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            base.MeasureOverride(availableSize);
            var totalHeight = content.DesiredSize.Height;
            if (DHeight > content.DesiredSize.Height)
            {
                if (SnappedToBottom) {spacer.Height = DHeight - totalHeight; bottomspacer.Height = 0;}
                else {bottomspacer.Height = DHeight - totalHeight; spacer.Height=0;}
            }
            else { spacer.Height = 0; bottomspacer.Height = 0;}
            return base.MeasureOverride(availableSize);
        }

        public override void SaveWindowPos()
        {
            if (double.IsNaN(Left)|| double.IsNaN(Top)) return;
            BasicTeraData.Instance.WindowData.PopupNotificationLocation = LastSnappedPoint ?? new Point(Left, Top);
        }
    }
}
