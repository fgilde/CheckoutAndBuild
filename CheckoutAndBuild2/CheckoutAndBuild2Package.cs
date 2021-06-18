using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using CheckoutAndBuild2.Contracts;
using EnvDTE;
using EnvDTE80;
using FG.CheckoutAndBuild2.Attributes;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using FG.CheckoutAndBuild2.VisualStudio.SearchProvider;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TeamFoundation.TeamExplorer.ConnectPage;
using Microsoft.VisualStudio.TeamFoundation.WorkItemTracking.Extensibility;
using Window = EnvDTE.Window;

namespace FG.CheckoutAndBuild2
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	///
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the 
	/// IVsPackage interface and uses the registration attributes defined in the framework to 
	/// register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true)]
	// This attribute is used to register the information needed to show this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", Const.Version, IconResourceID = 400)]
	[Guid(GuidList.guidCheckoutAndBuild2PkgString)]

	[ProvideSearchProvider(typeof(WorkItemSearchProvider), "WorkItemSearchProvider")]
	[ProvideOptionPage(typeof(CheckoutAndBuildOptionsPage), Const.ApplicationName, "COAB Main Options", 0, 0, false)]
	[ProvideOptionPage(typeof(CheckoutAndBuildSectionOptionsPage), Const.ApplicationName, "TeamExplorer Sections", 0, 0, false, new[] { "Section", "Visiblity", "Pages" })]
	[ProvideOptionPage(typeof(CheckoutAndBuildPluginsOptionsPage), Const.ApplicationName, "Plugins / Extensions", 0, 0, false, new[] { "Plugins", "Extensions", "Services" })]	
	[ProvideOptionPage(typeof(CheckoutAndBuildCopySettingsPage), Const.ApplicationName, "Copy/Export Settings", 0, 0, false, new[] { "Settings", "Extensions", "Workspace" })]	
	[ProvideOptionPage(typeof(WorkspaceSpecificOptionsPage), Const.ApplicationName, "Workspace Specific Settings", 0, 0, false, new[] { "Settings", "Extensions", "Workspace" })]	

	[ProvideMenuResource("Menus.ctmenu", 1)]
	
#if DEBUG
	//[ProvideExtensionRepository("{469a0cff-b232-4911-8f6f-0a81ce14abbf}", "http://cpair:7744/InmetaGallery/GalleryService.svc", 0x64, "VSGallery", "CP Gallery")]
	[ProvideExtensionRepository("{60926e9d-cfec-49b6-8706-43b6ce5a4df3}", "http://cptfs2013:82/GalleryService.svc", 0x64, "VSGallery", "CP Visual Studio Gallery")]
#endif
	#region Service Provides
	
	[ProvideService(typeof(TfsContext))]
	[ProvideService(typeof(ITfsContext))]
	[ProvideService(typeof(SettingsService))]
	[ProvideService(typeof(LocalBuildService))]
	[ProvideService(typeof(UnitTestService))]
	[ProvideService(typeof(CleanupService))]
	[ProvideService(typeof(CheckoutService))]
	[ProvideService(typeof(NugetRestoreService))]
	[ProvideService(typeof(MainLogic))]
	[ProvideService(typeof(MainViewModel))]
	[ProvideService(typeof(ExternalActionService))]
	[ProvideService(typeof(GlobalStatusService))]
	[ProvideService(typeof(CheckoutAndBuild2PackageManager))]
    [ProvideService(typeof(ScriptExportProvider))]
	[ProvideService(typeof(PowerShellExecutor))]

    #endregion
    //[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string)]
    [Export(typeof(IServiceProvider))]
	public sealed class CheckoutAndBuild2Package : Package
	{
		private static CheckoutAndBuild2Package package;
	    public static string[] SupportedProjectExtensions;

        internal CompositionContainer MefContainer { get; private set; }
		internal AggregateCatalog AggregateCatalog { get; private set; }
	
		/// <summary>
		/// Default constructor of the package.
		/// Inside this method you can place any initialization code that does not require 
		/// any Visual Studio service because at this point the package object is created but 
		/// not sited yet inside Visual Studio environment. The place to do all the other 
		/// initialization is the Initialize method.
		/// </summary>
		public CheckoutAndBuild2Package()
		{
			package = this;
            
            foreach (var attribute in GetType().GetAttributes<ProvideServiceAttribute>(false))
				((IServiceContainer)this).AddService(attribute.Service, CreateServiceCallback, true);
			InitCatalog();
		}

		private void InitCatalog()
		{
			AggregateCatalog = new AggregateCatalog();
			AggregateCatalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
		    var pluginDirectory = SettingsService.GetPluginDirectory();
		    foreach (var directory in Directory.GetDirectories(pluginDirectory, "*.*", SearchOption.AllDirectories))
				AggregateCatalog.Catalogs.Add(new DirectoryCatalog(directory, "*.dll"));								

			//var catalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
			MefContainer = new CompositionContainer(AggregateCatalog);
			MefContainer.ComposeParts();
		    foreach (ICheckoutAndBuildPlugin plugin in MefContainer.GetExportedValues<ICheckoutAndBuildPlugin>())
		        plugin.Init(new CheckoutAndBuildServiceProvider(this, MefContainer), pluginDirectory);
            
		}

		private object CreateServiceCallback(IServiceContainer container, Type serviceType)
		{
			return Activator.CreateInstance(serviceType, container);
		}

		public static IEnumerable<T> GetExportedValues<T>()
		{
			return package.MefContainer.GetExportedValues<T>();
		}

	    /// <summary>Gets type-based services from the VSPackage service container.</summary>
	    /// <param name="serviceType">The type of service to retrieve.</param>
	    /// <returns>An instance of the requested service, or null if the service could not be found.</returns>
	    /// <exception cref="T:System.ArgumentNullException">
	    /// <paramref name="serviceType" /> is null.</exception>
	    protected override object GetService(Type serviceType)
	    {
	        if (serviceType == typeof(ITfsContext))
	            serviceType = typeof(TfsContext);
	        return base.GetService(serviceType);
	    }

	    public static T GetGlobalService<T>()
			where T : class
		{
			if (typeof(T) == typeof(CheckoutAndBuild2Package))
				return package as T;
			var res = package.Get<T>();
			return res ?? package.MefContainer.GetExportedValueOrDefault<T>();
		}
	
		/////////////////////////////////////////////////////////////////////////////
		// Overridden Package Implementation
		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override void Initialize()
		{
			base.Initialize();
			//this.Get<CheckoutAndBuild2PackageManager>().BeginUpdateCheckAsync();
			var teamExplorer = this.Get<ITeamExplorer>();
		    if (GetGlobalService<SettingsService>().Get(SettingsKeys.EnableSectionManagement, false))
		    {
		        teamExplorer?.OnChange(() => teamExplorer.CurrentPage, SectionManager.UpdateSectionIncluded);
		    }

		    SupportedProjectExtensions = GetGlobalService<SettingsService>().Get(SettingsKeys.SupportedExtensions, Const.DefaultSupportedProjectExtensions);		    		    
		    AddCustomCommands();
			LoadResources();		    
        }

		protected override int QueryClose(out bool canClose)
	    {
	        GetGlobalService<SettingsService>().MakeBackup();

	        return base.QueryClose(out canClose);
	    }

		private void AddCustomCommands()
		{
			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if (null != mcs)
			{

				// Create the menu option in the error list window
				CommandID errorListCommand = new CommandID(GuidList.guidErrorListString.ToGuid(), (int)PkgCmdIDList.commandIdErrorList);
				OleMenuCommand errorMenuItem = new OleMenuCommand(ClearCOABErrors, errorListCommand);
				errorMenuItem.BeforeQueryStatus += errorMenuItem_BeforeQueryStatus;
				mcs.AddCommand(errorMenuItem);

				var replaceInWorkItemsCommand = new CommandID(GuidList.guidWorkItemSearchReplace.ToGuid(), (int)PkgCmdIDList.cmdidWorkItemSearchAndReplace);
				var menuItem = new MenuCommand(ReplaceInWorkItemsClick, replaceInWorkItemsCommand);
				mcs.AddCommand(menuItem);

			}
		}

		private void ReplaceInWorkItemsClick(object sender, EventArgs e)
		{
			/*
			var teamExplorer = GetGlobalService<ITeamExplorer>();
		    teamExplorer?.NavigateToPage(new Guid(GuidList.workItemSearchReplacePageId), teamExplorer.CurrentPage.GetService<IWorkItemQueriesExt>().SelectedQueryItems);
			*/
			var teamExplorer = GetGlobalService<ITeamExplorer>();
		    teamExplorer?.NavigateToPage(new Guid(GuidList.workItemSearchReplacePageId), teamExplorer.CurrentPage.GetService<IWorkItemQueriesExt2>().SelectedQueryIds);
		}

		private void errorMenuItem_BeforeQueryStatus(object sender, EventArgs e)
		{
			OleMenuCommand menuItem = sender as OleMenuCommand;
		    //var errorListId = WindowKinds.vsWindowKindErrorList;
		    var errorListId = "{D78612C7-9962-4B83-95D9-268046DAD23A}";
		    Window window = GetGlobalService<DTE>().Windows.Item(errorListId);
			ErrorList myErrorList = (ErrorList)window.Object;
			if (menuItem != null)
				menuItem.Visible = menuItem.Enabled = myErrorList.ErrorItems != null && myErrorList.ErrorItems.Count > 0;
		}

		private void ClearCOABErrors(object sender, EventArgs e)
		{
			Output.ClearTasks();
		}

		private void LoadResources()
		{
			if (Application.Current != null)
			{
				var viewModelTemplates = new ResourceDictionary
				{
					Source = new Uri("pack://application:,,,/CheckoutAndBuild2;component/Themes/Generic.xaml", UriKind.RelativeOrAbsolute)
				};
				Application.Current.Resources.MergedDictionaries.Add(viewModelTemplates);
			}
		}

		#endregion
	}
}
