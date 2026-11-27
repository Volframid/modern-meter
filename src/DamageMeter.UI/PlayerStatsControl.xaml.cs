using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace DamageMeter.UI
{
    public partial class PlayerStatsControl : INotifyPropertyChanged
    {
        private readonly DoubleAnimation _animation = new(0, TimeSpan.FromMilliseconds(100)) { EasingFunction = new QuadraticEase() };
        private double _targetDamageFactor = double.NaN;

        private bool _isHovered;
        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                if (_isHovered == value) return;
                _isHovered = value;
                N();
            }
        }

        public PlayerStatsControl()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += (_, _) =>
            {
                if (IsLoaded) ObserveDamage();
            };
        }

        private PlayerDamageViewModel? _observed;

        private void OnUnloaded(object sender, RoutedEventArgs e) => DisconnectDamage();

        private void OnLoaded(object sender, RoutedEventArgs e) => ObserveDamage();

        private void DisconnectDamage()
        {
            if (_observed != null) _observed.DamageFactorChanged -= OnDamageFactorChanged;
            _observed = null;
            _targetDamageFactor = double.NaN;
            DpsIndicator.BeginAnimation(RangeBase.ValueProperty, null);
        }

        private void ObserveDamage()
        {
            DisconnectDamage();
            if (DataContext is not PlayerDamageViewModel vm) return;
            _observed = vm;
            vm.DamageFactorChanged += OnDamageFactorChanged;
        }

        private void OnDamageFactorChanged(double factor)
        {
            if (!Dispatcher.CheckAccess())
            {
                var source = _observed;
                Dispatcher.InvokeAsync(() =>
                {
                    if (source is not null && ReferenceEquals(source, _observed)) OnDamageFactorChanged(factor);
                });
                return;
            }

            factor = double.IsFinite(factor) ? Math.Clamp(factor, 0, 1) : 0;
            if (factor == _targetDamageFactor) return;
            _targetDamageFactor = factor;
            _animation.To = factor;
            DpsIndicator.BeginAnimation(RangeBase.ValueProperty, _animation);
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            (Window.GetWindow(this) as ClickThrouWindow)?.Move(sender, e);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            IsHovered = true;
        }
        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            IsHovered = false;
        }

        #region INPC

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void N([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PlayerDamageViewModel dc)
            {
                dc.ShowSkillDetailsCommand.Execute(null);
            }
        }
    }
}
