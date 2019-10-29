using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Controls;
using Microsoft.TeamFoundation.WorkItemTracking.WpfControls;

namespace FG.CheckoutAndBuild2.Services
{
	public class WorkItemManager
	{
		private readonly TfsContext tfsContext;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public WorkItemManager(TfsContext tfsContext)
		{
			this.tfsContext = tfsContext;
		}

		public IEnumerable<Changeset> FindLinkedChangesets(int workItemId, Func<Changeset, bool> predicate = null)
		{
			return FindLinkedChangesets(WorkItemStore.GetWorkItem(workItemId), predicate);
		}

		public IEnumerable<Changeset> FindLinkedChangesets(WorkItem workItem, Func<Changeset, bool> predicate = null)
		{
            throw new NotSupportedException("TODO: Implement me again");
			//if (predicate == null)
			//	predicate = changeset => true;
		
			//return from extLink in workItem.Links.OfType<ExternalLink>() 
			//	   let artifact = LinkingUtilities.DecodeUri(extLink.LinkedArtifactUri) 
			//	   where String.Equals(artifact.ArtifactType, "Changeset", StringComparison.Ordinal)
			//	   select tfsContext.VersionControlServer.ArtifactProvider.GetChangeset(new Uri(extLink.LinkedArtifactUri)) into changeset where predicate(changeset) select changeset;
		}

		public WorkItemsListViewHelper WorkItemsListViewHelper { get { return WorkItemsListViewHelper.Instance; } }

		public WorkItemStore WorkItemStore => tfsContext.GetService<WorkItemStore>();

	    public Task<WorkItemCollection> RunQueryAsync(QueryDefinition query, WorkItemStore store = null)
		{
			return RunQueryAsync(query.QueryText, store);
		}

		public Task<WorkItemCollection> RunQueryAsync(string query, WorkItemStore store = null)
		{
			store = store ?? WorkItemStore;
			return Task.Run(() => Check.TryCatch<WorkItemCollection, Exception>(() => store.Query(query, Context)));
		}

		public Task PrintWorkItemsAsync(WorkItemCollection collection, CancellationToken cancellationToken = default(CancellationToken))
		{
			return WorkItemPrinting.WorkItemPrinter.PrintWorkItemsAsync(collection, cancellationToken);
		}

		public Task PrintWorkItemsAsync(WorkItem[] collection, CancellationToken cancellationToken = default(CancellationToken))
		{
			return WorkItemPrinting.WorkItemPrinter.PrintWorkItemsAsync(collection, cancellationToken);
		}

		public Task<WorkItemCollection> QueryUsersWorkItemsAsync(TeamFoundationIdentity identity)
		{
			return RunQueryAsync(GetDefaultUserWorkItemQuery(identity));
		}

		public void ShowQueryResults(QueryDefinition2 queryDefinition)
		{
			WorkItemsListViewHelper.ShowQueryResults(queryDefinition);
		}

		public void ShowQueryResults(string queryText)
		{		
			var workItemControlHost = tfsContext.GetService<IWorkItemControlHost>();
			workItemControlHost.ShowQueryResults(tfsContext.TeamProjectCollection, tfsContext.TfsContextManager.CurrentContext.TeamProjectName, queryText);
		}

		public void NavigateToWorkItem(Uri workItemUri)
		{
			NavigateToWorkItem(WorkItemStore.GetWorkItem(workItemUri));
		}

		public void NavigateToWorkItem(WorkItem workItem)
		{
			var workItemControlHost = tfsContext.GetService<IWorkItemControlHost>();
			workItemControlHost.ShowWorkItem(workItem);
		}

		public static string PrepareQueryText(string queryText)
		{
			return queryText.Replace("*", "[Microsoft.VSTS.Common.StackRank], [Microsoft.VSTS.Common.Priority],  [Microsoft.VSTS.Common.Severity], [System.State],  [System.Title] ");
		}


		public IEnumerable<WorkItemType> GetWorkItemTypes()
		{
			var projects = WorkItemStore.Projects.OfType<Project>();
			return projects.SelectMany(project => project.WorkItemTypes.OfType<WorkItemType>());
		}

		public string GetDefaultUserWorkItemQuery(params TeamFoundationIdentity[] identities)
		{
			string query = string.Empty;
			foreach (TeamFoundationIdentity identity in identities.Where(i => i != null))
			{
				if (string.IsNullOrEmpty(query))
					query = string.Format("[System.AssignedTo] = '{0}' ", identity.DisplayName);
				else
					query += string.Format("OR [System.AssignedTo] = '{0}' ", identity.DisplayName);
			}
			return (String.Format("SELECT [Microsoft.VSTS.Common.StackRank], [Microsoft.VSTS.Common.Priority],  [Microsoft.VSTS.Common.Severity], [System.State],  [System.Title]  FROM WorkItems WHERE ({0}) AND  [System.State] <> 'Closed'  AND  [System.State] <> 'Resolved'", query));
		}

		public IDictionary Context => new Dictionary<string, string>
		{
		    {"project", tfsContext.TfsContextManager.CurrentContext.TeamProjectName},
		    {"me", tfsContext.VersionControlServer.AuthorizedUser}
		};
	}
}