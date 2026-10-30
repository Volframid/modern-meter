using System.Threading.Tasks;
using System;
using System.Windows;
using Data;
using Data.Actions.Notify;
using System.Linq;
using System.Collections.Generic;
using Data.Actions.Notify.SoundElements;
using System.Windows.Threading;
using System.Windows.Media;
using Forms = System.Windows.Forms;
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
        private Task _soundQueue = Task.CompletedTask;
        private readonly Dictionary<Balloon, NotifyFlashMessage> _mountBalloons = new();
        private bool _placing;

        public double NotificationBackgroundOpacity
        {
            get => (double)GetValue(NotificationBackgroundOpacityProperty);
            private set => SetValue(NotificationBackgroundOpacityProperty, value);
        }
        public static readonly DependencyProperty NotificationBackgroundOpacityProperty = DependencyProperty.Register(
            nameof(NotificationBackgroundOpacity), typeof(double), typeof(PopupNotification), new PropertyMetadata(.5d));
        public Color NotificationBackgroundColor { get => (Color)GetValue(NotificationBackgroundColorProperty); private set => SetValue(NotificationBackgroundColorProperty, value); }
        public static readonly DependencyProperty NotificationBackgroundColorProperty = DependencyProperty.Register(nameof(NotificationBackgroundColor), typeof(Color), typeof(PopupNotification), new PropertyMetadata(Colors.Black));
        public Color NotificationBorderColor { get => (Color)GetValue(NotificationBorderColorProperty); private set => SetValue(NotificationBorderColorProperty, value); }
        public static readonly DependencyProperty NotificationBorderColorProperty = DependencyProperty.Register(nameof(NotificationBorderColor), typeof(Color), typeof(PopupNotification), new PropertyMetadata(Colors.White));

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
            SettingsWindowViewModel.WindowColorsChanged += OnNotificationColorsChanged;
            OnNotificationColorsChanged();
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

        private void OnNotificationColorsChanged()
        {
            if (Dispatcher.HasShutdownStarted) return;
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(OnNotificationColorsChanged)); return; }
            NotificationBackgroundColor = BasicTeraData.Instance.WindowData.BackgroundColor;
            NotificationBorderColor = BasicTeraData.Instance.WindowData.BorderColor;
        }

        protected override void OnClosed(EventArgs e)
        {
            SettingsWindowViewModel.NotificationOpacityChanged -= OnNotificationOpacityChanged;
            SettingsWindowViewModel.WindowColorsChanged -= OnNotificationColorsChanged;
            _focusTimer.Stop();
            base.OnClosed(e);
        }

        private void PresentNotification()
        {
            // Showing a card must not repeatedly demote/promote the HUD or activate it.
            Topmost = BasicTeraData.Instance.WindowData.Topmost;
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
            RemoveInactiveMountNotifications();
            if (!_gameForeground()) return;
            foreach (var balloon in notificationBalloons.ToSyncArray().Where(b => ModernNotificationPolicy.BackgroundOnly(b.EventType)))
                notificationBalloons.Remove(balloon);
        }

        public SynchronizedObservableCollection<Balloon> notificationBalloons;

        private void RemoveInactiveMountNotifications()
        {
            foreach (var entry in _mountBalloons.ToArray())
                if (!notificationBalloons.Contains(entry.Key) || !ModernMountNotifications.CanDeliver(entry.Value))
                { notificationBalloons.Remove(entry.Key); _mountBalloons.Remove(entry.Key); }
        }

        public void AddNotification(List<NotifyFlashMessage> flashList)
        {
            foreach(var flash in flashList.OrderByDescending(x => x.Priority).ThenByDescending(ModernMountNotifications.Weight))
            {
                AddNotification(flash);
            }
        }


        public void AddNotification(NotifyFlashMessage flash)
        {
            Dispatcher.Invoke(() =>
            {

                if (flash == null || !ModernMountNotifications.CanDeliver(flash)) { return; }
                RemoveInactiveMountNotifications();
                ModernMountNotifications.NormalizeIcon(flash);
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

                    if (ModernMountNotifications.Weight(flash) != 0) _mountBalloons[existing ?? flash.Balloon] = flash;
                    _present();
                    _focusTimer.Start();
                }

                if (!BasicTeraData.Instance.WindowData.MuteSound && flash.Sound != null)
                    _soundQueue = _soundQueue.ContinueWith(previous =>
                    {
                        if (previous.IsFaulted) _ = previous.Exception;
                        if (!BasicTeraData.Instance.WindowData.MuteSound && ModernMountNotifications.CanDeliver(flash) && (!backgroundOnly || !_gameForeground()))
                            _playSound(flash.Sound);
                    }, TaskScheduler.Default);
            });
        }

        internal void RemoveMe(Balloon context)
        {
            notificationBalloons.Remove(context);
        }

        public override void SnapToScreen()
        {
            if (_placing || IsDragging || !IsLoaded || !IsVisible) return;
            var anchor = LastSnappedPoint ?? new Point(Left, Top);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null || !double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y)) return;
            var transform = source.CompositionTarget.TransformToDevice;
            var dx = transform.M11; var dy = transform.M22;
            // A growing card must not choose a different monitor by HWND overlap.
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point(
                (int)Math.Round(anchor.X * dx), (int)Math.Round(anchor.Y * dy)));
            var bounds = TeraWindow.IsTeraFullScreen() ? screen.Bounds : screen.WorkingArea;
            var area = new Rect(bounds.Left / dx, bounds.Top / dy, bounds.Width / dx, bounds.Height / dy);
            var location = BasicTeraData.Instance.WindowData.SnapToBorders
                ? ModernNotificationPlacement.Place(anchor, new Size(ActualWidth, ActualHeight), area) : anchor;
            _placing = true;
            try
            {
                if (Math.Abs(Left - location.X) > .01) Left = location.X;
                if (Math.Abs(Top - location.Y) > .01) Top = location.Y;
                SnappedToBottom = location.Y < anchor.Y;
                _dragged = false;
            }
            finally { _placing = false; }
        }

        public override void SaveWindowPos()
        {
            if (double.IsNaN(Left)|| double.IsNaN(Top)) return;
            BasicTeraData.Instance.WindowData.PopupNotificationLocation = LastSnappedPoint ?? new Point(Left, Top);
        }
    }
}
