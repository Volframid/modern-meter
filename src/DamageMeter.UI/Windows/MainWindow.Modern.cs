using System.Windows;
using DamageMeter.UI.Windows;

namespace DamageMeter.UI;

public sealed partial class MainWindow
{
    private void OpenModernDashboard(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel model) ModernDashboard.ShowFor(model);
        e.Handled = true;
    }
}
