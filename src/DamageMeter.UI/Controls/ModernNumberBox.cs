using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DamageMeter.UI.Controls;

/// <summary>A bounded numeric editor with repeatable small-step arrows.</summary>
public sealed class ModernNumberBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ModernNumberBox),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (owner, _) => ((ModernNumberBox)owner).RefreshText(), CoerceNumber));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ModernNumberBox), new PropertyMetadata(1d));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ModernNumberBox),
        new PropertyMetadata(0d, (owner, _) => owner.CoerceValue(ValueProperty)), number => double.IsFinite((double)number));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ModernNumberBox),
        new PropertyMetadata(100d, (owner, _) => owner.CoerceValue(ValueProperty)), number => double.IsFinite((double)number));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    private readonly TextBox _editor;

    public ModernNumberBox()
    {
        MinWidth = 80;
        Height = 30;
        Cursor = Cursors.Arrow;
        var surface = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        surface.SetResourceReference(Border.BackgroundProperty, "ModernSurface");
        surface.SetResourceReference(Border.BorderBrushProperty, "ModernLine");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        _editor = new TextBox { Name = "ValueEditor", Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), Margin = new Thickness(6, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left, Cursor = Cursors.IBeam };
        _editor.SetResourceReference(TextBox.ForegroundProperty, "ModernAccent");
        _editor.LostKeyboardFocus += (_, _) => CommitText();
        _editor.PreviewKeyDown += OnEditorKeyDown;
        grid.Children.Add(_editor);
        var arrows = new Grid();
        arrows.RowDefinitions.Add(new RowDefinition());
        arrows.RowDefinitions.Add(new RowDefinition());
        Grid.SetColumn(arrows, 1);
        for (var i = 0; i < 2; i++)
        {
            var direction = i == 0 ? 1 : -1;
            var icon = new Path { Data = Geometry.Parse(i == 0 ? "M0,4 L4,0 L8,4" : "M0,0 L4,4 L8,0"),
                Width = 7, Height = 4, Stretch = Stretch.Fill, StrokeThickness = 1.2 };
            var button = new RepeatButton { Name = i == 0 ? "IncreaseValue" : "DecreaseValue", Content = icon,
                Focusable = false, Cursor = Cursors.Hand, Delay = 350, Interval = 100, Padding = new Thickness(0) };
            button.SetResourceReference(StyleProperty, "ModernNumberArrow");
            icon.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = button });
            button.Width = double.NaN;
            button.Height = double.NaN;
            Grid.SetRow(button, i);
            button.Click += (_, _) => Adjust(direction);
            arrows.Children.Add(button);
        }
        grid.Children.Add(arrows);
        surface.Child = grid;
        Content = surface;
        RefreshText();
    }

    private static object CoerceNumber(DependencyObject owner, object value)
    {
        var number = (double)value;
        var box = (ModernNumberBox)owner;
        return double.IsFinite(number) ? box.Clamp(number) : box.Value;
    }

    private double Clamp(double number) => Math.Clamp(number, Minimum, Math.Max(Minimum, Maximum));

    private void RefreshText()
    {
        if (_editor != null) _editor.Text = Value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private void CommitText()
    {
        const NumberStyles styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if ((double.TryParse(_editor.Text, styles, CultureInfo.CurrentCulture, out var number) ||
             double.TryParse(_editor.Text.Replace(',', '.'), styles, CultureInfo.InvariantCulture, out number)) && double.IsFinite(number))
            SetCurrentValue(ValueProperty, Clamp(number));
        RefreshText();
    }

    private void Adjust(int direction)
    {
        CommitText();
        var step = double.IsFinite(Step) && Step > 0 ? Step : 1;
        SetCurrentValue(ValueProperty, Clamp(Math.Round(Value + direction * step, 4)));
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitText();
        else if (e.Key == Key.Escape) RefreshText();
        else if (e.Key == Key.Up) Adjust(1);
        else if (e.Key == Key.Down) Adjust(-1);
        else return;
        e.Handled = true;
    }
}
