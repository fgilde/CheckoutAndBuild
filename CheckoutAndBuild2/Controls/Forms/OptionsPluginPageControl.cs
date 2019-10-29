using System;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.VisualStudio.Pages;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class OptionsPluginPageControl : UserControl
	{
		internal PlugInModel SelectedPlugin => listBoxPlugins.SelectedItem as PlugInModel;

	    internal CheckoutAndBuildPluginsOptionsPage OptionsPage { get; set; }

		public OptionsPluginPageControl()
		{			
			InitializeComponent();
			listBoxPlugins.DisplayMember = "Name";
		}
		
		public void Initialize()
		{			
			listBoxPlugins.Items.Clear();
			linkLabel.Tag = linkLabel.Text = SettingsService.GetPluginDirectory();
			UpdateLabel();
			var plugins = CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>().AggregateCatalog;
			foreach (DirectoryCatalog c in plugins.Catalogs.OfType<DirectoryCatalog>().Where(catalog => catalog.LoadedFiles.Any()))
			{
				var model = new PlugInModel(c.Path);
				if (!string.IsNullOrEmpty(model.Name) && model.Name != "de")
					listBoxPlugins.Items.Add(model);
			}
		}

		private void UpdateLabel()
		{		
			linkLabel.Text = FileHelper.ToShortPath((string) linkLabel.Tag, 60);
		}

		private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start((string) linkLabel.Tag);
		}

		private void listBoxPlugins_SelectedIndexChanged(object sender, EventArgs e)
		{
			buttonRemove.Enabled = SelectedPlugin != null;
		}

		private void buttonAdd_Click(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog { CheckFileExists = true, Filter = "Zip Files|*.zip|VS Extensions|*.vsix" };
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				string targetDir = Path.Combine(SettingsService.GetPluginDirectory(), Path.GetFileNameWithoutExtension(openFileDialog.FileName));
				var zipStorer = ZipStorer.Open(openFileDialog.FileName, FileAccess.Read);
				foreach (var zipFileEntry in zipStorer.ReadCentralDir())
					zipStorer.ExtractFile(zipFileEntry, Path.Combine(targetDir, zipFileEntry.FilenameInZip));
				ShowRestartNotification();
				listBoxPlugins.Items.Add(new PlugInModel(targetDir));
			}
		}

		private void buttonRemove_Click(object sender, EventArgs e)
		{
			if (SelectedPlugin != null && Directory.Exists(SelectedPlugin.Path))
			{			
				Directory.Delete(SelectedPlugin.Path, true);
				listBoxPlugins.Items.Remove(SelectedPlugin);
				ShowRestartNotification();
			}
		}

		private void ShowRestartNotification()
		{
			linkLabelNotification.Text = "Please Restart Visual Studio!";
			linkLabelNotification.Visible = true;
		}

		private void linkLabelNotification_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start(Application.ExecutablePath);
			Process.GetCurrentProcess().Kill();			
		}
	}
	

	internal class PlugInModel
	{
		public string Path { get; set; }

		public string Name => System.IO.Path.GetFileName(Path);

	    /// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public PlugInModel(string path)
		{
			Path = path;
		}
	}
}