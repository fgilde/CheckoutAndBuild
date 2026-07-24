using System.Windows;
using CheckoutAndBuild2.Contracts;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Types
{
	public class ChangesetInfo : NotificationObject
	{
		private readonly string ownerName;
		private Changeset changeset;
		private IBuildDetail triggeredBuildDetail;
		private Visibility buildDetailVisibility;

		/// <summary>
		/// Initializes a new instance of the <see cref="NotificationObject"/> class.
		/// </summary>
		public ChangesetInfo(Changeset changeset, string ownerName)
		{
			this.ownerName = ownerName ?? changeset.OwnerDisplayName;
			Changeset = changeset;
		}

		public string OwnerDisplayName
		{
			get { return ownerName; }
		}

		public Visibility BuildDetailVisibility
		{
			get { return buildDetailVisibility; }
			set { SetProperty(ref buildDetailVisibility, value); }
		}

		public IBuildDetail TriggeredBuildDetail
		{
			get { return triggeredBuildDetail; }
			private set { SetProperty(ref triggeredBuildDetail, value); }
		}

		public Changeset Changeset
		{
			get { return changeset; }
			set
			{
				if(SetProperty(ref changeset, value))
					UpdateBuildDetail();
			}
		}

		public string Comment
		{
			get { return Changeset != null ? Changeset.Comment : string.Empty; }
		}

		private async void UpdateBuildDetail()
		{
			var tfsContext = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
			if (tfsContext.BuildDetailManager.IsBackgroundBuildDefinitionLoadingEnabled && changeset != null)
			{
				BuildDetailVisibility = Visibility.Visible;
				TriggeredBuildDetail = await tfsContext.BuildDetailManager.FindBuildDetailForChangesetAsync(changeset);
				if (TriggeredBuildDetail != null)
					TriggeredBuildDetail.StatusChanged += (sender, args) => RaisePropertyChanged(() => TriggeredBuildDetail);
			}
			else
			{
				BuildDetailVisibility = Visibility.Collapsed;
			}
		}
		 
	}
}