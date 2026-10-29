using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Data;

namespace DamageMeter.UI.Windows
{
    /// <summary>
    /// Interaction logic for UpdatePopup.xaml
    /// </summary>
    public partial class UpdatePopup : ClickThrouWindow
    {
        public new bool? DialogResult { get; set; } // idk why we can't use normal DialogResult and need to do that thing manually
        public UpdatePopup()
        {
            SizeToContent = SizeToContent.Manual;
            InitializeComponent();
        }
        private void ClickThrouWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CloseWindow.Source = BasicTeraData.Instance.ImageDatabase.Close.Source;
            Browser.Markdown=GetPatchNotes();
        }

        private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter.ToString().StartsWith("http")) Process.Start("explorer.exe", e.Parameter.ToString());
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void StartButton_OnClick(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }

        private string GetPatchNotes()
        {
            try
            {
                return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));
            }
            catch (IOException) { return "Modern Meter updates are managed by TERA Toolbox."; }
            catch (UnauthorizedAccessException) { return "Modern Meter updates are managed by TERA Toolbox."; }

        }

    }
}
