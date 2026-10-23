using Data;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace DamageMeter.UI.Windows
{
    public partial class SettingsWindow
    {
        private static SettingsWindow? _window;
        private static bool _visible;

        public static SettingsWindowViewModel GetViewModel()
        {
            _window ??= new SettingsWindow();
            return (SettingsWindowViewModel)_window.DataContext;
        }

        public SettingsWindow() : this(true) { }

        // The isolated render harness loads the real templates without starting a capture session.
        public SettingsWindow(bool createViewModel)
        {
            InitializeComponent();
            if (createViewModel) DataContext = new SettingsWindowViewModel();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Application.Current.Dispatcher.HasShutdownStarted) return;
            e.Cancel = true;
            CloseWindow(this, new RoutedEventArgs());
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            BasicTeraData.Instance.WindowData.Save();
            BasicTeraData.Instance.HotkeysData.Save();
            Hide();
            _visible = false;
        }

        /// <summary>
        /// Creates the window without showing it, to avoid instantiating it every time it should be showed, avoiding click lag
        /// </summary>
        public static void Create()
        {
            _window = new SettingsWindow();
        }

        public static void ShowWindow()
        {
            if (_visible) { if (_window?.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal; _window?.Activate(); return; }
            _visible = true;
            _window ??= new SettingsWindow(); // it should never be the case, as the window is created only once via Create() method at startup
            _window.Show();
            _window.Activate();
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.LeftShift) return;
            ((SettingsWindowViewModel)DataContext).IsShiftDown = true;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.LeftShift) return;
            ((SettingsWindowViewModel)DataContext).IsShiftDown = false;
        }
    }
}
