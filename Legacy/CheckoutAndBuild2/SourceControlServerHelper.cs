using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.Server;
using Microsoft.VisualStudio.TeamFoundation;
using Microsoft.VisualStudio.TeamFoundation.TeamExplorer.ConnectPage;
using OperationCanceledException = System.OperationCanceledException;

namespace FG.CheckoutAndBuild2
{
	public class SourceControlServerHelper
	{
        /*
		private static List<ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData>> teamProjectExtendedInfoProviders { get; set; }
		private static IServiceProvider serviceProvider => CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>();
	    private static readonly TaskQueue queue = new TaskQueue();
		private static Dictionary<string, ExportLifetimeContext<IConnectPageExtendedProjectInfoProvider>> providerExportLifetimeContexts;
		private static readonly Dictionary<string, IConnectPageExtendedProjectInfoProvider> extendedInfoProviders = new Dictionary<string, IConnectPageExtendedProjectInfoProvider>();

		public static async Task<List<ConnectServerInfo>> GetRegisteredConnectionsAsync(List<Exception> exceptions = null)
		{			
			return await queue.QueueWorkAsync(cancellationToken =>
			{				
				ImportInfoProvidersFromServiceProvider(cancellationToken);
				return GetServerInfosForRegisteredConnections(cancellationToken, exceptions ?? new List<Exception>());
			});
		}

		public static void AddExtendedInfoProvider(string name, IConnectPageExtendedProjectInfoProvider provider, CancellationToken cancellationToken)
		{			
			extendedInfoProviders.AddOrUpdate(name, provider);
			//provider.PageRefreshRequired += new EventHandler<ConnectPageRefreshRequiredEventArgs>(this.provider_RefreshRequired);
		}

		private static List<ConnectServerInfo> GetServerInfosForRegisteredConnections(CancellationToken cancellationToken, List<Exception> exceptions)
		{
			List<ConnectServerInfo> list = new List<ConnectServerInfo>();
			RegisteredProjectCollection[] projectCollections = RegisteredTfsConnections.GetProjectCollections();
			Dictionary<string, ConnectServerInfo> dictionary = new Dictionary<string, ConnectServerInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (RegisteredProjectCollection rpc in projectCollections)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					ConnectServerInfo connectServerInfo;
					if (!dictionary.TryGetValue(rpc.Uri.Authority, out connectServerInfo))
					{
						string uriString = rpc.Uri.AbsoluteUri;
						if (rpc.Uri.AbsolutePath.Length > 0)
							uriString = uriString.Substring(0, uriString.Length - rpc.Uri.AbsolutePath.Length);
						connectServerInfo = new ConnectServerInfo(new Uri(uriString));
						dictionary[rpc.Uri.Authority] = connectServerInfo;
						list.Add(connectServerInfo);
						TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.GetServerInfosForRegisteredConnections server uri={0}", (object)connectServerInfo.Uri);
					}
					ConnectCollectionInfo connectCollectionInfo = CollectionInfoFromRegisteredProjectCollection(rpc, cancellationToken, exceptions);
					if (connectServerInfo != null)
					{
						if (connectCollectionInfo != null)
						{
							connectServerInfo.IsHosted = connectCollectionInfo.IsHosted;
							connectServerInfo.Collections.Add(connectCollectionInfo);
							connectServerInfo.Projects.AddRange(connectCollectionInfo.Projects);
						}
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					TeamFoundationTrace.Error(TFPackageTraceKeywordSets.ConnectPage, ex);
					exceptions.Add(ex);
				}
			}
			return list;
		}

		private static void ImportInfoProvidersFromServiceProvider(CancellationToken cancellationToken)
		{
			providerExportLifetimeContexts = new Dictionary<string, ExportLifetimeContext<IConnectPageExtendedProjectInfoProvider>>();
			teamProjectExtendedInfoProviders = serviceProvider.Get<List<ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData>>>();			
			foreach (ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData> exportFactory in teamProjectExtendedInfoProviders)
			{
				ExportLifetimeContext<IConnectPageExtendedProjectInfoProvider> export = exportFactory.CreateExport();
				export.Value.ServiceProvider = serviceProvider;
				providerExportLifetimeContexts.Add(exportFactory.Metadata.Name, export);
			}
			foreach (KeyValuePair<string, ExportLifetimeContext<IConnectPageExtendedProjectInfoProvider>> keyValuePair in providerExportLifetimeContexts)
				AddExtendedInfoProvider(keyValuePair.Key, keyValuePair.Value.Value, cancellationToken);
		}

		private static ConnectCollectionInfo CollectionInfoFromRegisteredProjectCollection(RegisteredProjectCollection rpc, CancellationToken cancellationToken, List<Exception> exceptions)
		{
			Guid collectionId;
			if (!TeamExplorerProjects.Instance.TryGetCollectionId(rpc.Uri.ToString(), out collectionId))
				return null;
			Dictionary<string, string> collectionProperties = TeamExplorerProjects.Instance.GetCollectionProperties(collectionId);
			Guid teamForCollection = TeamExplorerProjects.Instance.GetActiveTeamForCollection(collectionId);
			ReadOnlyCollection<CatalogNode> projectCatalogNodes = null;
			string a;
			collectionProperties.TryGetValue("CollectionIsHosted", out a);
			bool isHosted = !string.IsNullOrEmpty(a) && string.Equals(a, bool.TrueString, StringComparison.OrdinalIgnoreCase);
			ConnectCollectionInfo collection = new ConnectCollectionInfo(rpc.Uri, collectionId, rpc.Name, isHosted, teamForCollection);
			//TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.CollectionInfoFromRegisteredProjectCollection collection name={0} isHosted={1} id={2}", (object)collection.Name, (object)(bool)(collection.IsHosted ? 1 : 0), (object)collection.Id);
			foreach (string str in TeamExplorerProjects.Instance.GetProjectsForCollection(collectionId))
			{
				try
				{
					if (collection.ContainsProject(str))
					{
						TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.CollectionInfoFromRegisteredProjectCollection ignoring duplicate project entry uri={0}", (object)str);
					}
					else
					{
						Dictionary<string, string> projectProperties = TeamExplorerProjects.Instance.GetProjectProperties(collectionId, str);
						int capFlagsScc = 0;
						string projectName;
						projectProperties.TryGetValue("ProjectName", out projectName);
						if (!string.IsNullOrEmpty(projectName))
						{
							string s;
							if (projectProperties.TryGetValue("ProjectCapFlagsScc", out s))
								int.TryParse(s, out capFlagsScc);
						}
						else
						{
							TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.RefreshServersAsync project has no cached data uri={0}", (object)str);
							TryGetProjectInfoFromCurrentContext(collectionId, str, ref projectCatalogNodes, out projectName, out capFlagsScc);
							if (!string.IsNullOrEmpty(projectName))
								TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.CollectionInfoFromRegisteredProjectCollection project data read from current context uri={0}", (object)str);
							else
								continue;
						}
						cancellationToken.ThrowIfCancellationRequested();
						IEnumerable<ConnectPageProjectSubItem> connectPageProjectSubItems = null;
						foreach (KeyValuePair<string, IConnectPageExtendedProjectInfoProvider> keyValuePair in extendedInfoProviders)
						{
						    IConnectPageExtendedProjectInfoProvider infoProvider = keyValuePair.Value;
						    infoProvider.Refresh(true, cancellationToken);                            
							connectPageProjectSubItems = infoProvider.GetProjectSubItems(collectionId, str);
							if (connectPageProjectSubItems != null && connectPageProjectSubItems.Any())
								break;
						}
					    var connectPageProjectExtendedInfo = new ConnectPageProjectExtendedInfo();
                        connectPageProjectExtendedInfo.SubItems.AddRange(connectPageProjectSubItems);
                        ConnectProjectInfo connectProjectInfo = new ConnectProjectInfo(str, projectName, capFlagsScc, collection, connectPageProjectExtendedInfo);
						collection.Projects.Add(connectProjectInfo);
						TeamFoundationTrace.Verbose(TFPackageTraceKeywordSets.ConnectPage, "ConnectPageViewModel.CollectionInfoFromRegisteredProjectCollection project name={0} capFlagsScc={1} uri={2}", (object)connectProjectInfo.Name, (object)connectProjectInfo.CapFlagsScc.ToString(), (object)connectProjectInfo.Uri);
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					TeamFoundationTrace.Error(TFPackageTraceKeywordSets.ConnectPage, ex);
					exceptions.Add(ex);
				}
			}
			return collection;
		}


		private static void TryGetProjectInfoFromCurrentContext(Guid collectionId, string projectUri, ref ReadOnlyCollection<CatalogNode> projectCatalogNodes, out string projectName, out int projectCapFlagsScc)
		{
			projectName = null;
			projectCapFlagsScc = 0;			
			ITeamFoundationContextManager service = serviceProvider.Get<ITeamFoundationContextManager>();
			if (service != null && service.CurrentContext != null && (service.CurrentContext.HasCollection && service.CurrentContext.TeamProjectCollection.InstanceId == collectionId))
			{
				TfsTeamProjectCollection projectCollection = service.CurrentContext.TeamProjectCollection;
				if (projectCatalogNodes == null)
					projectCatalogNodes = projectCollection.CatalogNode.QueryChildren(new[]
					{
						TeamProjectCatalogConstants.ResourceType
					}, null, false, CatalogQueryOptions.None);
				if (projectCatalogNodes != null)
				{
					CatalogNode catalogNode1 = null;
					foreach (CatalogNode catalogNode2 in projectCatalogNodes)
					{
						string y;
						if (catalogNode2.Resource != null && catalogNode2.Resource.Properties != null && (catalogNode2.Resource.Properties.TryGetValue("ProjectUri", out y) && TFStringComparer.ProjectUri.Equals(projectUri, y)))
						{
							catalogNode1 = catalogNode2;
							break;
						}
					}
					if (catalogNode1 != null)
					{
						catalogNode1.Resource.Properties.TryGetValue("ProjectName", out projectName);
						string s;
						if (catalogNode1.Resource.Properties.TryGetValue("SourceControlCapabilityFlags", out s))
							int.TryParse(s, out projectCapFlagsScc);
					}
				}
			}
		}
        */

	}
}