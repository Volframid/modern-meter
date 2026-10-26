using System.Windows;
using System.Windows.Controls;
using Data;

namespace DamageMeter.UI;

// Shared widths are scoped to one meter/preview party, never to unrelated windows.
// Each slot retains the same column across roles; the fifth slot is the right edge.
public sealed class CompactMetricsPanel : Grid
{
    public CompactMetricsPanel()
    {
        for (var i = 0; i < 4; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CompactMetric" + i });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CompactMetric4" });
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        ArrangeSlots();
    }

    internal void ArrangeSlots()
    {
        var slot = 0;
        foreach (UIElement child in Children)
        {
            if (child is not ContentControl cell || cell.Tag is not Metric metric) continue;
            var index = slot++;
            var hidden = metric is Metric.None or Metric.Dps;
            cell.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            if (hidden) continue;
            var last = index == 4;
            SetColumn(cell, last ? 5 : index);
            cell.HorizontalContentAlignment = last ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            cell.Margin = last ? new Thickness(5, 0, 0, 0) : new Thickness(0, 0, 5, 0);
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
