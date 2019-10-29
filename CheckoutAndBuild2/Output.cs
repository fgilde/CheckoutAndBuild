using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FG.CheckoutAndBuild2
{ 
	public static class Output
	{
		private static LogLevel? logLevel = null;
		static Output()
		{			
			Trace.Listeners.Add(new VisualStudioOutputTraceListener());
		}

		private static readonly Lazy<ErrorListProvider> errorListProvider = new Lazy<ErrorListProvider>(() => new ErrorListProvider(CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>())
			{
				ProviderName = Const.ApplicationName,
				ProviderGuid = GuidList.errorProviderGuid.ToGuid()
			});

		public static ErrorListProvider GetErrorListProvider()
		{
			return errorListProvider.Value;
		}

		public static LogLevel LogLevel
		{
			get { return logLevel ?? (logLevel = CheckoutAndBuild2Package.GetGlobalService<SettingsService>().Get(SettingsKeys.LogLevelKey, LogLevel.Quiet)).Value; }
			set { logLevel = value; }
		}

		public static IVsOutputWindowPane GetOutputWindowPane()
		{
			IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
			
			if(outWindow == null)
				return null;
			const string customTitle = Const.ApplicationName;
			Guid customGuid = GuidList.outputPaneGuid.ToGuid();
			IVsOutputWindowPane customPane;
			outWindow.GetPane(ref customGuid, out customPane);
			if(customPane == null)
				outWindow.CreatePane(ref customGuid, customTitle, 1, 1);
			outWindow.GetPane(ref customGuid, out customPane);
			return customPane;
		}


	    public static void NotificationError(string message)
	    {
            Notification(message, NotificationType.Error);
	    }

	    public static void NotificationInfo(string message)
	    {
	        Notification(message);
	    }

        public static void Notification(string message, NotificationType notificationType, NotificationFlags flags, Action action)

		{
		    var id = Guid.NewGuid();
		    ICommand cmd = null;
		    if (action != null)
		        cmd = new DelegateCommand<object>(o =>
		        {
                    HideNotification(id);
		            action();
		        });
            Notification(message, notificationType, flags, cmd, id);
		}

		public static void Notification(string message, NotificationType notificationType = NotificationType.Information, NotificationFlags flags = NotificationFlags.None, ICommand command = null, 
			Guid id = default(Guid))
		{
		    if (!string.IsNullOrWhiteSpace(message))
		    {
		        if (id == default(Guid))
		            id = Guid.NewGuid();
		        var explorer = CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>();
		        explorer.ShowNotification(message, notificationType, flags, command, id);
		    }
		}
        
		public static void Exception(Exception ex)
		{
			var aggregateEx = ex as AggregateException;
			if (aggregateEx != null)
			{
				aggregateEx.Unwrap().Apply(Exception);
			}
			else
			{
				//TeamFoundationTrace.Error(TFPackageTraceKeywordSets.General, ex);
				WriteLine("Error: {0}", ex.Message);
				//Notification(ex.Message, NotificationType.Error);
			}
		}

		public static void WriteWarning(string message,
			string fileName, int lineNumber = 0)
		{
			var task = new ErrorTask
			{
				Category = TaskCategory.User,
				Document = fileName,
				Line = lineNumber,
				ErrorCategory = TaskErrorCategory.Warning,
				Text = message
			};

			AddTask(task);
		}

		public static void WriteError(string message, string fileName, int lineNumber = 0,
			int columnNumber = 0,
			string helpKeyword = null, TaskCategory taskCategory = TaskCategory.User,
			string projectFile = null,
			bool allowOpenFile = false,
			bool activatePane = true)
		{
			SimpleVsHierarchy hierarchyItem = projectFile != null ? new SimpleVsHierarchy(projectFile) : null;
			ErrorTask task = new ErrorTask
			{
				HierarchyItem = hierarchyItem,
				Category = taskCategory,
				Document = fileName,
				Line = lineNumber,
				ErrorCategory = TaskErrorCategory.Error,
				Text = message,
				Column = columnNumber,			
				HelpKeyword = helpKeyword
			};

			if (allowOpenFile)
				task.Navigate += (sender, args) => VisualStudioDTE.TryOpenFile(fileName, lineNumber, hierarchyItem);

			AddTask(task, activatePane);
		}

		public static IEnumerable<ErrorTask> FindErrorTasks(ISolutionProjectModel project)
		{
			var errors = new List<ErrorTask>();
			foreach (var projectPath in project.GetSolutionProjects().Select(p => p.FullPath).Concat(project.ToSolution().Projects.Select(solutionProject => solutionProject.ProjectName).Distinct()))
				errors.AddRange(FindErrorTasks(null, projectPath));

			return errors;
		} 

		public static IEnumerable<ErrorTask> FindErrorTasks(string fileName = null, string projectName = null)
		{
			return errorListProvider.Value.Tasks.OfType<ErrorTask>()
				.Where(task => (string.IsNullOrEmpty(fileName) || task.Document == fileName || task.Document == Path.GetFileName(fileName)) &&
								(string.IsNullOrEmpty(projectName) ||
								(task.HierarchyItem is SimpleVsHierarchy && ((SimpleVsHierarchy) task.HierarchyItem).ProjectFile == projectName ||
								((SimpleVsHierarchy) task.HierarchyItem).ProjectFile == Path.GetFileName(projectName))));
		}

		public static bool HasTask()
		{
			return errorListProvider.Value.Tasks.Count > 0;
		}

		public static void ActivateErrorList(bool condition = true)
		{
			if(condition)
				errorListProvider.Value.Show();
		}
		
		public static void AddTask(Task task, bool activatePane = true)
		{
			errorListProvider.Value.Tasks.Add(task);
			if (activatePane)
				ActivateErrorList();
		}

		public static void WriteLine(string text)
		{
			WriteLine(text, new object[0]);
		}

		public static void WriteLine(string text, params object[] args)
		{
			if (LogLevel != LogLevel.Disabled)
			{
				if (text == null)
					text = "";
				if (args != null && args.Any())
					text = string.Format(text, args);
				var pane = GetOutputWindowPane();
				if (pane != null)
				{
					//pane.OutputStringThreadSafe(text + Environment.NewLine);
					pane.OutputString(text + Environment.NewLine);
					pane.Activate();
				}
			}
		}

		public static void ClearTasks(bool condition = true)
		{
			if(condition)
				errorListProvider.Value.Tasks.Clear();
		}

		public static void HideNotification(Guid id)
		{
			TeamExplorerUtils.Instance.HideNotification(CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>(), id);
		}

	    public static void ActionLink(string text, Action action, NotificationType notification = NotificationType.Information, 
            string tooltip = "Click here to execute")
	    {
            Guid id = Guid.NewGuid();
            var cmd = new DelegateCommand<object>(param =>
            {
                HideNotification(id);
                action();
            }, param => true);

            Notification($"[{text}]({tooltip})", notification, NotificationFlags.None, cmd, id);
        }
	}

	public class VisualStudioOutputTraceListener : TraceListener
	{
		private readonly MainLogic logic;
		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Diagnostics.TraceListener"/> class.
		/// </summary>
		public VisualStudioOutputTraceListener()
		{
			logic = CheckoutAndBuild2Package.GetGlobalService<MainLogic>();
		}

		/// <summary>
		/// When overridden in a derived class, writes the specified message to the listener you create in the derived class.
		/// </summary>
		/// <param name="message">A message to write. </param>
		public override void Write(string message)
		{
			WriteLine(message);
		}

		/// <summary>
		/// When overridden in a derived class, writes a message to the listener you create in the derived class, followed by a line terminator.
		/// </summary>
		/// <param name="message">A message to write. </param>
		public override void WriteLine(string message)
		{
			if (logic.IsAnyServiceRunning)
				Output.WriteLine(message);
		}
	}
}