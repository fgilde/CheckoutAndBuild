using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Controls
{
	public class CheckInDialog
	{
		#region Properties and Variables
		public DialogResult DialogResult { get; set; }
		//private Workspace workSpace = null;
		private VersionControlServer versionControlServer = null;
		private readonly WorkingFolder workingFolder;
		private Form checkInDialog = null;
		private PropertyInfo checkedPendingChanges = null;
		private PropertyInfo checkedWorkItems = null;
		private PropertyInfo checkinNotes = null;
		private PropertyInfo comment = null;
		private PropertyInfo policyFailureOverrideReason = null;
		private PropertyInfo policyFailures = null;
		private PendingChange[] changes;
		#endregion

		#region Constructor
		public CheckInDialog(VersionControlServer versionControlServer, WorkingFolder workingFolder = null)
		{
			this.versionControlServer = versionControlServer;
			this.workingFolder = workingFolder;
		}

		public CheckInDialog(VersionControlServer versionControlServer, PendingChange[] changes)
		{
			this.versionControlServer = versionControlServer;
			this.changes = changes;
		}

		#endregion


		#region ShowDialog
		public bool ShowDialog(Workspace workSpace = null,
			int pageToSelect = 0,
			bool? closeButtonOnly = null)
		{

			if (closeButtonOnly == null)
				closeButtonOnly = pageToSelect == 4;


			if (workSpace == null)
				workSpace = versionControlServer.GetWorkspace(Environment.MachineName, versionControlServer.AuthorizedUser);

			workSpace.Refresh();
			//Thread.Sleep(2000);
			PendingChange[] pendingChange = workSpace.GetPendingChanges();
			PendingChange[] checkedinPendingChange;
			if (workingFolder != null)
			{
				 checkedinPendingChange = workSpace.GetPendingChanges(workingFolder.ServerItem, RecursionType.Full);
			}
			else if (changes != null && changes.Any())
			{
				checkedinPendingChange = changes;
			}
			else
			{
				checkedinPendingChange = pendingChange;
			}
			//PendingChange[] checkedinPendingChange = pendingChange.Where(c => c.ServerItem.Contains(workingFolder.ServerItem)).ToArray();
			//Assembly controlsAssembly = Assembly.LoadFile(controlsAssemblyPath);
			Assembly controlsAssembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			Type vcCheckinDialogType = controlsAssembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogCheckin");


			ConstructorInfo ci = vcCheckinDialogType.GetConstructor(
				   BindingFlags.Instance | BindingFlags.NonPublic,
				   null,
				   new Type[] { typeof(Workspace), typeof(PendingChange[]), typeof(PendingChange[]), 
			   typeof(string), typeof(CheckinNote), typeof(WorkItemCheckedInfo[]), typeof(string) },
				   null);
			checkInDialog = (Form)ci.Invoke(new object[] { workSpace, pendingChange, checkedinPendingChange, "", null, null, "" });

			checkedPendingChanges = vcCheckinDialogType.GetProperty("CheckedChanges", BindingFlags.Instance | BindingFlags.NonPublic);
			checkedWorkItems = vcCheckinDialogType.GetProperty("CheckedWorkItems", BindingFlags.Instance | BindingFlags.NonPublic);
			checkinNotes = vcCheckinDialogType.GetProperty("CheckinNotes", BindingFlags.Instance | BindingFlags.NonPublic);
			comment = vcCheckinDialogType.GetProperty("Comment", BindingFlags.Instance | BindingFlags.NonPublic);
			policyFailureOverrideReason = vcCheckinDialogType.GetProperty("PolicyFailureOverrideReason", BindingFlags.Instance | BindingFlags.NonPublic);
			policyFailures = vcCheckinDialogType.GetProperty("PolicyFailures", BindingFlags.Instance | BindingFlags.NonPublic);

			if (Application.OpenForms.Count > 0 && Application.OpenForms[0] != null)
			{
				checkInDialog.Owner = Application.OpenForms[0];
				checkInDialog.StartPosition = FormStartPosition.CenterParent;				
			}

			dynamic dynamicForm = ExposedObject.From(checkInDialog);
			dynamic checkinsChannelControl = ExposedObject.From(dynamicForm.m_channelControl); // PendingCheckinsChannelControl
			//dynamic conflictsChannel = ExposedObject.From(((dynamic)checkinsChannelControl).ConflictsControl);
			//dynamic conflictsPresenter = ExposedObject.From(((dynamic)conflictsChannel).m_conflictPresenter);
			//dynamic conflictStore = ExposedObject.From(((dynamic)conflictsPresenter).m_store);

			if (pageToSelect != 0)
			{
				checkinsChannelControl.SelectedChannel = pageToSelect;
				var channel = checkinsChannelControl.SelectedChannel;
			}

			if (closeButtonOnly == true)
			{
				try
				{
					dynamicForm.buttonOK.Visible = false;
					dynamicForm.buttonCancel.Text = "Close";
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}

			checkInDialog.ShowDialog();
			this.DialogResult = checkInDialog.DialogResult;
			System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

			if (DialogResult != DialogResult.Cancel)
			{

				PendingChange[] selectedPendingChange = (PendingChange[])checkedPendingChanges.GetValue(checkInDialog, null);
				WorkItemCheckinInfo[] checkedWorkItemInfo = (WorkItemCheckinInfo[])checkedWorkItems.GetValue(checkInDialog, null);
				string comments = (string)comment.GetValue(checkInDialog, null);
				string policyReason = (string)policyFailureOverrideReason.GetValue(checkInDialog, null);
				CheckinNote notes = (CheckinNote)checkinNotes.GetValue(checkInDialog, null);
				PolicyFailure[] failures = (PolicyFailure[])policyFailures.GetValue(checkInDialog, null);

				PolicyOverrideInfo overrideinfo = new PolicyOverrideInfo(policyReason, failures);

				try
				{
					workSpace.CheckIn(selectedPendingChange,
								comments,
								notes,
								checkedWorkItemInfo,
								overrideinfo);
				}
				catch (Exception exp)
				{
					System.Windows.Forms.MessageBox.Show("Check in Failed due to " + exp.Message);
					return false;
				}
			}
			else
			{
				return true;
			}


			return true;
		}
		#endregion

	}
}