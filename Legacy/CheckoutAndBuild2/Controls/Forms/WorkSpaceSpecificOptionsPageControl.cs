using System;
using System.Windows.Forms;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class WorkSpaceSpecificOptionsPageControl : UserControl
	{
		private readonly TfsContext tfsContext;
		
		public WorkSpaceSpecificOptionsPageControl()
		{
			InitializeComponent();			
			tfsContext = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
		}

		internal void Initialize()
		{			
			LoadWorkspaces();
		}

		private void LoadWorkspaces()
		{
			var isConnected = tfsContext.IsTfsConnected;
			elementHost.Enabled = comboBoxWorkspaceSelect.Enabled = isConnected;
			if (isConnected)
			{
				comboBoxWorkspaceSelect.Items.Clear();				
				comboBoxWorkspaceSelect.Items.AddRange(tfsContext.GetWorkspaces());
				comboBoxWorkspaceSelect.SelectedItem = tfsContext.SelectedWorkspace;
			}
		}

		private void comboBoxWorkspaceSelect_SelectedIndexChanged(object sender, EventArgs e)
		{
			LoadWorkspaceSpecificSettings(comboBoxWorkspaceSelect.SelectedItem as Workspace);
		}

		private void LoadWorkspaceSpecificSettings(Workspace workspace)
		{
			tfsContext.SelectedWorkspace = workspace;
			var model = new ServiceSettingsSelectorViewModel(tfsContext.ServiceProvider);
			model.LoadUIControls();
			elementHost.Child = new ServiceSettingsContentView {DataContext = model, Background = new SolidColorBrush(BackColor.ToMediaColor())};
		}
	}
}
