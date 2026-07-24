using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Properties;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.ExtensionManager;

namespace FG.CheckoutAndBuild2.WorkItemPrinting
{
	public static class WorkItemPrinter
	{
		public const string guidWorkItemPrintingVSPackagePkgString = "c33fbb62-196e-4b67-bf19-169d53ab6f3b";
			
		public static bool IsExternalWorkItemPrinterInstalled
		{
			get { return GetWorkItemPrinterExtension() != null; }
		}

		public static IInstalledExtension GetWorkItemPrinterExtension()
		{
			IInstalledExtension extension;
			return CheckoutAndBuild2Package.GetGlobalService<IVsExtensionManager>().TryGetInstalledExtension(guidWorkItemPrintingVSPackagePkgString, out extension) ? extension : null;
		}

		public static Task PrintWorkItemsAsync(WorkItemCollection workItemCollection, CancellationToken cancellationToken = default(CancellationToken))
		{
			return PrintWorkItemsAsync(workItemCollection.OfType<WorkItem>().ToArray(), cancellationToken);
		}

		public static Task PrintWorkItemsAsync(WorkItem[] workItems, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.Run( async () =>
			{
				foreach (IGrouping<string, WorkItem> group in from wi in workItems group wi by wi.Type.Name.ToLowerInvariant() into g select g)
				{
					List<StoryCard> storyCards = @group.Select(workItem => new StoryCard(workItem.Id, workItem.IterationPath, workItem.Title)).ToList();
					 					
					int maxColumns = 2;
					string fileName = Path.GetTempFileName() + ".docx";
                    switch (group.Key)
                    {
                        case "bug":
                            File.WriteAllBytes(fileName, Resources.bug_card);
                            break;
                        case "task":
                            File.WriteAllBytes(fileName, Resources.task_card);
                            break;
                        case "user story":
                            File.WriteAllBytes(fileName, Resources.story_card);
                            maxColumns = 1;
                            break;
                        case "feature":
                            File.WriteAllBytes(fileName, Resources.feature_card);
                            maxColumns = 1;
                            break;
                        default:
                            throw new Exception("Unbekannte WorkItemType");
                    }

                    var helper = new StoryCardHelper(fileName, maxColumns);
					helper.AddCards(storyCards);
					string toFilename = Path.GetTempFileName();
					var newFile = Path.ChangeExtension(toFilename, @"docx");
					File.Move(toFilename, newFile);
					helper.SaveToFile(newFile);

					await ScriptHelper.ExecuteScriptAsync(newFile, string.Empty, ScriptExecutionSettings.NormalProcess, cancellationToken: cancellationToken);
					File.Delete(fileName);
				}
			}, cancellationToken);			
		}
	}
}