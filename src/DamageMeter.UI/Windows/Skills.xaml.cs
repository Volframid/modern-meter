using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using DamageMeter.Database.Structures;
using Data;
using Lang;
using Tera.Game.Abnormality;
using System.Windows.Threading;

namespace DamageMeter.UI
{
    public partial class Skills
    {
        private Buff _buff;
        private SkillsLog _skillDealtLog;
        private SkillsDetail _skillDps;
        private SkillsDetail _skillHeal;
        private SkillsDetail _skillMana;
        private SkillsDetail _skillCounter;
        private SkillsLog _skillReceivedLog;

        private Database.Structures.Skills _skills;
        private bool _initialPlacementPending = true;
        private bool _centering;


        public Skills(PlayerDamageViewModel vm)
        {
            DataContext = vm;

            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ContentRendered += (_, _) => CompleteInitialPlacement();
            SizeChanged += (_, _) => CenterInitialPlacement();
            // Once the user interacts or drags, later data/layout updates must
            // never pull the window away from the chosen position.
            PreviewMouseLeftButtonDown += (_, _) => _initialPlacementPending = false;

        }


        public void Update(PlayerDamageDealt playerDamageDealt, EntityInformation entityInformation, Database.Structures.Skills skills, PlayerAbnormals buffs, bool timedEncounter)
        {
            if (_skills != null && skills == null) { return; }

            _skills = skills;
            var death = buffs.Death;
            if (death == null)
            {
                DeathCounter.Text = "0";
                DeathDuration.Text = "0" + LP.Seconds;
            }
            else
            {
                DeathCounter.Text = death.Count(entityInformation.BeginTime, entityInformation.EndTime).ToString();
                var duration = death.Duration(entityInformation.BeginTime, entityInformation.EndTime);
                var interval = TimeSpan.FromTicks(duration);
                DeathDuration.Text = interval.ToString(@"mm\:ss");
            }
            var aggro = buffs.Aggro(entityInformation.Entity);
            if (aggro == null)
            {
                AggroCounter.Text = "0";
                AggroDuration.Text = "0" + LP.Seconds;
            }
            else
            {
                AggroCounter.Text = aggro.Count(entityInformation.BeginTime, entityInformation.EndTime).ToString();
                var duration = aggro.Duration(entityInformation.BeginTime, entityInformation.EndTime);
                var interval = TimeSpan.FromTicks(duration);
                AggroDuration.Text = interval.ToString(@"mm\:ss");
            }

            _skillDps = new SkillsDetail(SkillAggregate.GetAggregate(playerDamageDealt, entityInformation.Entity, _skills, timedEncounter, Database.Database.Type.Damage), Database.Database.Type.Damage);
            DpsPanel.Content = _skillDps;
            _skillHeal = new SkillsDetail(SkillAggregate.GetAggregate(playerDamageDealt, entityInformation.Entity, _skills, timedEncounter, Database.Database.Type.Heal), Database.Database.Type.Heal);
            HealPanel.Content = _skillHeal;
            _skillMana = new SkillsDetail(SkillAggregate.GetAggregate(playerDamageDealt, entityInformation.Entity, _skills, timedEncounter, Database.Database.Type.Mana), Database.Database.Type.Mana);
            ManaPanel.Content = _skillMana;
            _buff = new Buff(playerDamageDealt, buffs);
            BuffPanel.Content = _buff;

            _skillCounter = new SkillsDetail(SkillAggregate.GetAggregate(playerDamageDealt, entityInformation.Entity, _skills, timedEncounter, Database.Database.Type.Counter), Database.Database.Type.Counter);
            CounterPanel.Content = _skillCounter;
            _skillDealtLog = new SkillsLog(_skills.GetSkillsDealt(playerDamageDealt.Source.User, entityInformation.Entity, timedEncounter), false);
            SkillsDealtPanel.Content = _skillDealtLog;
            _skillReceivedLog = new SkillsLog(_skills.GetSkillsReceived(playerDamageDealt.Source.User, timedEncounter), true);
            SkillsReceivedPanel.Content = _skillReceivedLog;
            CompleteInitialPlacement();
        }

        private void CompleteInitialPlacement()
        {
            if (!_initialPlacementPending) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!_initialPlacementPending || !IsLoaded) return;
                UpdateLayout();
                CenterInitialPlacement();
                if (_skills != null) _initialPlacementPending = false;
            }));
        }

        private void CenterInitialPlacement()
        {
            if (!_initialPlacementPending || _centering || IsDragging || !IsLoaded) return;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null || ActualWidth < 1 || ActualHeight < 1) return;
            _centering = true;
            try
            {
                var main = System.Windows.Application.Current?.MainWindow;
                var handle = main == null ? IntPtr.Zero : new WindowInteropHelper(main).Handle;
                var screen = handle == IntPtr.Zero ? Screen.PrimaryScreen : Screen.FromHandle(handle);
                if (screen == null) return;
                var work = screen.WorkingArea;
                var physical = new Rect(work.X, work.Y, work.Width, work.Height);
                var transform = source.CompositionTarget.TransformFromDevice;
                var logical = Rect.Transform(physical, transform);
                MaxWidth = logical.Width; MaxHeight = logical.Height;
                UpdateLayout();
                var position = ModernWindowPlacement.Center(physical, new Size(ActualWidth, ActualHeight), transform);
                Left = position.X; Top = position.Y; LastSnappedPoint = position;
            }
            finally { _centering = false; }
        }

        private void ClickThrouWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //todo: use svg for these
            DeathIcon.Source = BasicTeraData.Instance.ImageDatabase.Skull.Source;
            DeathTimeIcon.Source = BasicTeraData.Instance.ImageDatabase.SkullTime.Source;
            AggroIcon.Source = BasicTeraData.Instance.ImageDatabase.BossGage.Source;
            AggroTimeIcon.Source = BasicTeraData.Instance.ImageDatabase.AggroTime.Source;
            
        }
    }
}