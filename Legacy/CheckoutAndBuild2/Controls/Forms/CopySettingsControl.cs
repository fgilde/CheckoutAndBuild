using System;
using System.Linq;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
    public partial class CopySettingsControl : UserControl
    {
        private readonly SettingsService settingsService;
        private TfsContext tfs;

        public CopySettingsControl()
        {
            InitializeComponent();
            settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
        }

        public WorkingProfile SelectedSourceProfile
        {
            get => comboBoxSourceProfile.SelectedItem as WorkingProfile;
            set => comboBoxSourceProfile.SelectedItem = value;
        }

        public WorkingProfile SelectedTargetProfile
        {
            get => comboBoxTargetProfile.SelectedItem as WorkingProfile;
            set => comboBoxTargetProfile.SelectedItem = value;
        }

        public Workspace SelectedTargetWorkspace
        {
            get => comboBoxTargetWorkspace.SelectedItem as Workspace;
            set => comboBoxTargetWorkspace.SelectedItem = value;
        }

        public Workspace SelectedSourceWorkspace
        {
            get => comboBoxSourceWorkspace.SelectedItem as Workspace;
            set => comboBoxSourceWorkspace.SelectedItem = value;
        }

        public void Initialize(WorkingProfile selectedSourceProfile = null, Workspace selectedSourceWorkSpace = null, 
            WorkingProfile selectedTargetProfile = null, Workspace selectedTargetWorkSpace = null)
        {
            labelResult.Visible = false;

            tfs = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();

            var profiles = WorkingProfile.LoadProfiles();
            comboBoxSourceProfile.Items.Clear();
            comboBoxTargetProfile.Items.Clear();
            comboBoxSourceProfile.Items.AddRange(profiles);
            comboBoxTargetProfile.Items.AddRange(profiles);
            comboBoxSourceProfile.SelectedItem = selectedSourceProfile ?? profiles.FirstOrDefault();
            comboBoxTargetProfile.SelectedItem = selectedTargetProfile ?? profiles.FirstOrDefault();

            if (tfs.IsTfsConnected)
            {
                var workspaces = tfs.GetWorkspaces();
                comboBoxTargetWorkspace.Items.Clear();
                comboBoxSourceWorkspace.Items.Clear();
                comboBoxTargetWorkspace.Items.AddRange(workspaces);
                comboBoxSourceWorkspace.Items.AddRange(workspaces);
                comboBoxSourceWorkspace.SelectedItem = selectedSourceWorkSpace ?? workspaces.FirstOrDefault();
                comboBoxTargetWorkspace.SelectedItem = selectedTargetWorkSpace ??
                                                       workspaces.FirstOrDefault(
                                                           w => w != comboBoxSourceWorkspace.SelectedItem);
            }
            else
            {
                comboBoxSourceWorkspace.Enabled = false;
                comboBoxTargetWorkspace.Enabled = false;
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {            
            labelResult.Visible = false;            
            if (MessageBox.Show($"Copy settings from '{SelectedSourceProfile.Name}' profile and '{SelectedSourceWorkspace?.Name}' Workspace to '{SelectedTargetProfile.Name}' profile and '{SelectedTargetWorkspace?.Name}' Workspace?", "Copy Settings",
                    MessageBoxButtons.OKCancel) == DialogResult.OK)
            {                            
                var hasChangedSettings = settingsService.CopySettings(SelectedSourceProfile, SelectedSourceWorkspace, SelectedTargetProfile, SelectedTargetWorkspace);
                labelResult.Visible = true;
                if (hasChangedSettings && tfs.SelectedWorkspace == SelectedTargetWorkspace && (tfs.SelectedProfile.Id == SelectedTargetProfile.Id))
                    CheckoutAndBuild2Package.GetGlobalService<MainViewModel>().Update();                
            }
        }

        private void Combobox_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            labelResult.Visible = false;
            var count = settingsService.GetPropertiesForWorkspace(SelectedSourceProfile, SelectedSourceWorkspace).Count();
            labelAffected.Text = $"{count} Affected Settings";
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            settingsService.Export();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var hasChangedSettings = settingsService.Import();
            if (hasChangedSettings)
                CheckoutAndBuild2Package.GetGlobalService<MainViewModel>().Update();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("All CheckoutAndBuild Settings will be restored to default values. ", "Reset Settings", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                settingsService.Reset();                
            }
        }
    }
}
