using Data;
using Data.Actions.Notify;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Xceed.Wpf.AvalonDock.Controls;

namespace DamageMeter.UI
{
    /// <summary>
    /// Logica di interazione per NotificationControl.xaml
    /// </summary>
    public partial class NotificationControl : UserControl
    {
        public NotificationControl()
        {
            InitializeComponent();
            Unloaded += (_, _) => ReleaseLifetime();
        }
        Balloon _context;
        Timer _close;
        private int _lifetime;
        private bool _closing;
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ReleaseLifetime();
            _context = (Balloon)DataContext;
            _context.PropChanged += _context_RefreshEvent;
            ApplyEventIcon(_context.EventType);
            Height = double.NaN;
            BeginAnimation(HeightProperty, null);
            var an = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            this.BeginAnimation(OpacityProperty, an);
            RestartCloseTimer();
        }

        private void RestartCloseTimer()
        {
            if (_close != null) { _close.Stop(); _close.Elapsed -= _close_Elapsed; _close.Dispose(); }
            _close = new Timer(_context.DisplayTime);
            _close.Elapsed += _close_Elapsed;
            _close.Start();
        }

        private void ReleaseLifetime()
        {
            _lifetime++;
            _closing = false;
            if (_context != null) _context.PropChanged -= _context_RefreshEvent;
            _context = null;
            if (_close != null)
            {
                _close.Stop(); _close.Elapsed -= _close_Elapsed; _close.Dispose(); _close = null;
            }
            BeginAnimation(HeightProperty, null);
        }

        // Shared by the real Loaded path and isolated icon rendering checks.
        private void ApplyEventIcon(EventType eventType)
        {
            Color col;
            switch (eventType)
            {
                case EventType.MissingAb:
                    col = Colors.Red;
                    break;
                case EventType.AddRemoveAb:
                    col = Colors.Orange;
                    break;
                case EventType.Cooldown:
                    col = Color.FromRgb(0xfd, 0x39, 0x20);
                    break;
                case EventType.AFK:
                    col = Color.FromRgb(0x2, 0xbb, 0xff);
                    break;
                case EventType.Whisper:
                    col = Color.FromRgb(0xff, 0x87, 0xb3);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Whisper.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;
                    break;
                case EventType.MatchingSuccess:
                    col = Color.FromRgb(0x2d, 0xff, 0x73);
                    img.Source = BasicTeraData.Instance.ImageDatabase.DoneCircle.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;
                    break;
                case EventType.ReadyCheck:
                    col = Color.FromRgb(0x2d, 0xff, 0x73);
                    img.Source = BasicTeraData.Instance.ImageDatabase.DoneCircle.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;
                    break;
                case EventType.OtherUserApply:
                    col = Color.FromRgb(0x2d, 0xff, 0x73);
                    img.Source = BasicTeraData.Instance.ImageDatabase.GroupAdd.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.Broker:
                    col = Color.FromRgb(0xbf, 0x54, 0x26);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Money.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.PartyInvite:
                    col = Color.FromRgb(0x2d, 0xff, 0x73);
                    img.Source = BasicTeraData.Instance.ImageDatabase.GroupAdd.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.Trade:
                    col = Color.FromRgb(0xbf, 0x54, 0x26);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Money.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.GenericContract:
                    col = Color.FromRgb(0x2, 0xbb, 0xff);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Info.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.VanguardCredits:
                    col = Color.FromRgb(0xde, 0x48, 0xf2);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Credits.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.WakeUp:
                    col = Color.FromRgb(0x48, 0xf2, 0x94);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Info.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                case EventType.Mention:
                    col = Color.FromRgb(0x48, 0xf2, 0x94);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Info.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;

                    break;
                default:
                    col = Color.FromRgb(0x2, 0xbb, 0xff);
                    img.Source = BasicTeraData.Instance.ImageDatabase.Info.Source;
                    ell.Visibility = Visibility.Hidden;
                    img.Visibility = Visibility.Visible;
                    break;
            }

        }

        private void _context_RefreshEvent(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.InvokeIfRequired(() =>
            {
                if (!ReferenceEquals(sender, _context)) return;
                if (e.PropertyName == "Refresh")
                {
                    _lifetime++; _closing = false;
                    BeginAnimation(HeightProperty, null); Height = double.NaN;
                    RestartCloseTimer();
                }
                else if (e.PropertyName == "BodyText") TextBlock.Text = _context.BodyText;
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void _close_Elapsed(object sender, ElapsedEventArgs e)
        {
            var version = _lifetime;
            var context = _context;
            Dispatcher.InvokeIfRequired(() =>
            {
                if (version != _lifetime || context == null || _closing || !ReferenceEquals(sender, _close)) return;
                _close.Stop(); _closing = true;
                var h = this.ActualHeight;
                var an = new DoubleAnimation(h, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuadraticEase() };
                an.Completed += (s, ev) =>
                {
                    if (version != _lifetime || !ReferenceEquals(_context, context)) return;
                    var parent = ((UIElement)this).FindLogicalAncestor<PopupNotification>();
                    parent?.RemoveMe(context);
                    ReleaseLifetime();
                };
                //this.BeginAnimation(WidthProperty, an);
                this.BeginAnimation(HeightProperty, an);
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
    }
}
