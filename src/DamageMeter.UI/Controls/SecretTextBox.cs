using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DamageMeter.UI.Controls;

/// <summary>Every server token starts concealed and is revealed only by an explicit click.</summary>
public sealed class SecretTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SecretTextBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsRevealed { get; private set; }
    private readonly TextBox _editor = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0) };
    // Both states use the surrounding server card's ordinary TextBox style.
    // A fixed placeholder never exposes the actual token or its length.
    private readonly TextBox _mask = new() { Text = "••••••••••••••••", IsReadOnly = true,
        Focusable = false, IsTabStop = false, Cursor = Cursors.Hand, Margin = new Thickness(0) };
    private Window? _owner;
    private bool _updating;

    public SecretTextBox()
    {
        var grid = new Grid(); grid.Children.Add(_editor); grid.Children.Add(_mask); Content = grid;
        _mask.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; Reveal(); };
        _editor.TextChanged += (_, _) => { if (IsRevealed && !_updating) SetCurrentValue(TextProperty, _editor.Text); };
        _editor.KeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Enter) { Conceal(); Keyboard.ClearFocus(); e.Handled = true; } };
        LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (!IsKeyboardFocusWithin) Conceal(); }));
        DataContextChanged += (_, _) => Conceal();
        IsVisibleChanged += (_, _) => Conceal();
        Loaded += (_, _) => { Conceal(); AttachOwner(Window.GetWindow(this)); };
        Unloaded += (_, _) => { Conceal(); AttachOwner(null); };
    }

    public void Reveal()
    {
        if (!IsEnabled) return;
        IsRevealed = true; _updating = true; _editor.Text = Text ?? ""; _updating = false;
        _mask.Visibility = Visibility.Collapsed; _editor.Visibility = Visibility.Visible;
        _editor.Focus(); _editor.CaretIndex = _editor.Text.Length;
    }

    public void Conceal()
    {
        IsRevealed = false; _editor.Visibility = Visibility.Collapsed;
        // Plain text is absent from the masked editor and its automation value.
        _updating = true; _editor.Clear(); _updating = false; _mask.Visibility = Visibility.Visible;
    }

    private void AttachOwner(Window? window)
    {
        if (_owner != null) { _owner.Deactivated -= OnDeactivated; _owner.PreviewMouseDown -= OnOwnerMouseDown; }
        _owner = window;
        if (_owner != null) { _owner.Deactivated += OnDeactivated; _owner.PreviewMouseDown += OnOwnerMouseDown; }
    }
    private void OnDeactivated(object? sender, EventArgs e) => Conceal();
    private void OnOwnerMouseDown(object sender, MouseButtonEventArgs e) { if (IsRevealed && !IsMouseOver) Conceal(); }
    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (SecretTextBox)sender;
        if (!control.IsRevealed || control._updating) return;
        control._updating = true; control._editor.Text = e.NewValue as string ?? ""; control._updating = false;
    }
}
