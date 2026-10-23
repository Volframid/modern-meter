using System.Windows;
using System.Windows.Controls;
using Data;

namespace DamageMeter.UI;

// Shared widths are scoped to one meter/preview party, never to unrelated windows.
// Pack visible slots so different role configurations do not leave phantom columns.
public sealed class CompactMetricsPanel : Grid
{
    public CompactMetricsPanel()
    {
        for (var i = 0; i < 5; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CompactMetric" + i });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 5; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CompactDeath" + i });
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        ArrangeSlots();
    }

    internal void ArrangeSlots()
    {
        var left = 0;
        var right = 10;
        foreach (UIElement child in Children)
        {
            if (child is not ContentControl cell || cell.Tag is not Metric metric) continue;
            var hidden = metric is Metric.None or Metric.Dps;
            cell.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            if (hidden) continue;
            var death = metric == Metric.Deaths;
            SetColumn(cell, death ? right-- : left++);
            cell.HorizontalContentAlignment = death ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            cell.Margin = death ? new Thickness(5, 0, 0, 0) : new Thickness(0, 0, 5, 0);
        }
    }
}

public sealed class CompactMetricCell : ContentControl
{
    static CompactMetricCell()
    {
        TagProperty.OverrideMetadata(typeof(CompactMetricCell), new FrameworkPropertyMetadata(null, OnMetricChanged));
    }

    private static void OnMetricChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (LogicalTreeHelper.GetParent(sender) is CompactMetricsPanel panel) panel.ArrangeSlots();
    }
}
