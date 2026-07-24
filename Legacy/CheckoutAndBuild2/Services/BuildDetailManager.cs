using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Services
{
	public class BuildDetailManager : NotificationObject
	{
		private readonly TfsContext tfsContext;
		private Task<IBuildDetail[]> getBuildsTask;
		private CancellationTokenSource cancellationTokenSource;
		private List<IBuildDetail> buildDetails;
		private readonly SettingsService settingsService;

		public bool IsBackgroundBuildDefinitionLoadingEnabled { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public BuildDetailManager(TfsContext tfsContext)
		{
			this.tfsContext = tfsContext;
			settingsService = tfsContext.GetService<SettingsService>();
			tfsContext.ConnectionChanged += TfsContextOnConnectionChanged;
			var isEnabled = settingsService.Get(SettingsKeys.LoadBuildInformationsKey, false);
			SetEnabled(isEnabled);
			UpdateContainingBuilds();
		}

		private void TfsContextOnConnectionChanged(object sender, ContextChangedEventArgs e)
		{
			UpdateContainingBuilds();
		}

		public IBuildServer BuildServer => tfsContext.BuildServer;

	    public bool AllBuildDetailsLoaded => buildDetails != null && buildDetails.Count > 0;


	    public async Task<IBuildDetail> FindBuildDetailForChangesetAsync(Changeset changeset)
		{
			foreach (var buildDetail in await QueryBuildsAsync())
			{
				var chString = buildDetail.SourceGetVersion.Replace("C", "");
				if (changeset.ChangesetId.ToString() == chString)
					return buildDetail;
				var changesets = InformationNodeConverters.GetAssociatedChangesets(buildDetail);
				if (changesets.Any(summary => summary.ChangesetId == changeset.ChangesetId))
					return buildDetail;
			}
			return null;
		}


		public void SetEnabled(bool value)
		{			
			settingsService.Set(SettingsKeys.LoadBuildInformationsKey, value);
			if (IsBackgroundBuildDefinitionLoadingEnabled != value)
			{
				IsBackgroundBuildDefinitionLoadingEnabled = value;
				if (IsBackgroundBuildDefinitionLoadingEnabled)
				{
					if ((buildDetails == null || buildDetails.Count <= 0) &&
						(getBuildsTask == null || getBuildsTask.IsCompleted || getBuildsTask.IsCanceled))
						UpdateContainingBuilds();
				}
				else
				{
					FreeMemory();
				}
				RaisePropertyChanged(() => IsBackgroundBuildDefinitionLoadingEnabled);
			}
		}

		public void FreeMemory()
		{
			if (cancellationTokenSource != null && getBuildsTask != null && !getBuildsTask.IsCompleted)
				cancellationTokenSource.Cancel();
			if (getBuildsTask != null)
			{
				getBuildsTask.Dispose();
				getBuildsTask = null;
			}
		    buildDetails?.Clear();
		    GC.Collect();
		}


		public Task<IBuildDetail[]> QueryBuildsAsync()
		{
			if (AllBuildDetailsLoaded)
				return Task.Run(() => buildDetails.ToArray());
			//if (getBuildsTask.IsCompleted || getBuildsTask.Exception != null || getBuildsTask.IsCanceled)
			//	UpdateContainingBuilds();
			return getBuildsTask ?? Task.Run(() => new IBuildDetail[0]);
		}

		public Task<IBuildDetail[]> QueryBuildsForUserAsync(string user, int maxItems = 0)
		{
			return QueryBuildsAsync(CreateDetailSpec(user, maxItems));
		}

		public Task<IBuildDetail[]> QueryLastBuildsAsync()
		{
			if(AllBuildDetailsLoaded)
				return QueryBuildsAsync();
			var buildDetailSpec = CreateDetailSpec(null, 1);
			buildDetailSpec.MinFinishTime = DateTime.Now.AddHours(-12);
			buildDetailSpec.MaxFinishTime = DateTime.Now;
			buildDetailSpec.QueryOptions = QueryOptions.Workspaces;
			return QueryBuildsAsync(buildDetailSpec);
		}

		public Task<IBuildDetail[]> QueryBuildsAsync(IBuildDetailSpec detailSpec)
		{
			return Task.Run(() =>
			{
				var buildServer = BuildServer;
				if (buildServer != null)
					return Check.TryCatch<IBuildDetail[], Exception>(() => buildServer.QueryBuilds(detailSpec).Builds);
				return new IBuildDetail[0];
			});
		}


		private void UpdateContainingBuilds()
		{
			bool isConnected = tfsContext.IsTfsConnected;
			if (isConnected && IsBackgroundBuildDefinitionLoadingEnabled)
			{
				if (cancellationTokenSource != null && getBuildsTask != null && !getBuildsTask.IsCompleted)
					cancellationTokenSource.Cancel();
				cancellationTokenSource = new CancellationTokenSource();
				getBuildsTask = Task.Run(() =>
				{
					var buildServer = BuildServer;
					if (buildServer != null)
						return Check.TryCatch<IBuildDetail[], Exception>(() => buildServer.QueryBuilds(CreateDetailSpec()).Builds);											
					return new IBuildDetail[0];
				}, cancellationTokenSource.Token).IgnoreCancellation(cancellationTokenSource.Token);
				getBuildsTask.ContinueWith(task =>
				{
					if (!task.IsCanceled && task.Exception == null && task.Result != null)
						buildDetails = new List<IBuildDetail>(task.Result);
					Task.Delay(TimeSpan.FromHours(1)).ContinueWith(task1 => UpdateContainingBuilds(), cancellationTokenSource.Token);
				}, TaskContinuationOptions.NotOnCanceled);
			}
		}

		private IBuildDetailSpec CreateDetailSpec(string user = "", int maxBuildsPerDefinition = 0)
		{
			var buildServer = BuildServer;
			var buildDetailSpec = buildServer.CreateBuildDetailSpec(tfsContext.TfsContextManager.CurrentContext.TeamProjectName);
			if (maxBuildsPerDefinition > 0)
				buildDetailSpec.MaxBuildsPerDefinition = 10;
			if (!string.IsNullOrEmpty(user))
				buildDetailSpec.RequestedFor = user;
			buildDetailSpec.QueryOrder = BuildQueryOrder.FinishTimeDescending;			
			buildDetailSpec.Status = BuildStatus.All;
			buildDetailSpec.DefinitionSpec.TriggerType = DefinitionTriggerType.All;
			buildDetailSpec.Reason = BuildReason.All;
			return buildDetailSpec;
		}

	}
}