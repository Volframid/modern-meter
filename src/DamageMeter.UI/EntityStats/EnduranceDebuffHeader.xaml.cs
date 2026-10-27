using System.Windows;
using System.Windows.Input;
using Lang;

namespace DamageMeter.UI.EntityStats
{
    /// <summary>
    ///     Logique d'interaction pour EnduranceDebuff.xaml
    /// </summary>
    public partial class EnduranceDebuffHeader
    {
        public EnduranceDebuffHeader()
        {
            InitializeComponent();

            LabelClass.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "NativeText_Class");
            LabelAbnormalityDuration.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "NativeText_EffectTime");
            LabelInterval.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "NativeText_Fight");
            LabelAbnormalityDurationPercentage.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "NativeText_FightPercent");
            LabelName.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "NativeText_Name");
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) { ((ClickThrouWindow)Window.GetWindow(this))?.Move(sender, e); }
    }
}