using System;
using System.Windows;
using System.Windows.Controls;
using Tera.Game;

namespace DamageMeter.UI.Windows;

public partial class ModernDashboard : Window
{
    private static ModernDashboard? _window;
    public ModernDashboard(object model)
    {
        InitializeComponent();
        DataContext = model;
        if (model is MainViewModel)
        {
            AppearancePanel.DataContext = SettingsWindow.GetViewModel();
            Closed += (_, _) => Data.BasicTeraData.Instance.WindowData.Save();
        }
    }

    public static void ShowFor(MainViewModel model)
    {
        if (_window == null)
        {
            _window = new ModernDashboard(model) { Owner = Application.Current.MainWindow };
            _window.Closed += (_, _) => _window = null;
        }
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseDashboard(object sender, RoutedEventArgs e) => Close();

    private void SelectEncounter(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel || e.AddedItems.Count != 1) return;
        if (e.AddedItems[0] is not NpcEntity npc) return;
        NpcEntity? encounter = npc == MainViewModel.TotalEncounter ? null : npc;
        if (encounter != PacketProcessor.Instance.Encounter) PacketProcessor.Instance.NewEncounter = encounter;
    }

    private void ResetEncounter(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel) PacketProcessor.Instance.NeedToReset = true;
    }

    private void ExportJson(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel)
            PacketProcessor.Instance.NeedToExport = DataExporter.Dest.Json | DataExporter.Dest.Manual;
    }
}
