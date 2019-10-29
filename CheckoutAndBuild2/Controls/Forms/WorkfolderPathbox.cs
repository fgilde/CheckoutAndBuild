using System;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class WorkfolderPathbox : TextBox
	{
		private WorkingFolder workingFolder;

		public event EventHandler<EventArgs<string>> FolderBrowsed;

		private void RaiseFolderBrowsed(string e)
		{
			EventHandler<EventArgs<string>> handler = FolderBrowsed;
			if (handler != null) handler(this, new EventArgs<string>(e));
		}

		public bool EditLocalValue { get { return !Text.StartsWith("$"); } }

		public Workspace Workspace { get; set; }

		public TfsContext TfsHelper { get; set; }

		public WorkingFolder WorkingFolder
		{
			get { return workingFolder; }
			set
			{
				workingFolder = value;
				if (value != null)
					Text = EditLocalValue ? workingFolder.LocalItem : workingFolder.ServerItem;
				else
					Text = string.Empty;
			}
		}

		public WorkfolderPathbox()
		{
			InitializeComponent();
			buttonBrowse.Click += ButtonBrowseOnClick;
			buttonBrowse.Cursor = Cursors.Arrow;
		}

		private void ButtonBrowseOnClick(object sender, EventArgs eventArgs)
		{
			if(EditLocalValue)
			{
				var dlg = new FolderBrowserDialog {SelectedPath = Text, Description = "Select Local Directory",ShowNewFolderButton = true};
				if (dlg.ShowDialog() == DialogResult.OK)
				{
					RaiseFolderBrowsed(dlg.SelectedPath);
					Text = dlg.SelectedPath;
				}
			}else
			{
				//string newPath = TeamControlFactory.BrowseServerFolder(Workspace, Text);
				var item = TeamControlFactory.ShowDialogChooseItem(Text);
				if (item != null)
				{
					string newPath = item.ServerItem;
					if (!string.IsNullOrEmpty(newPath))
					{
						RaiseFolderBrowsed(newPath);
						Text = newPath;
					}
				}
			}
		}
	}
}
