using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ExtensionManager;

namespace FG.CheckoutAndBuild2.Services
{
	public class CheckoutAndBuild2PackageManager 
	{
		private readonly IServiceProvider serviceProvider;
		private IVsExtensionManager extensionManager;
		private readonly SettingsService settingsService;
	
		public CheckoutAndBuild2PackageManager(IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider;
			settingsService = serviceProvider.Get<SettingsService>();
		}

		public Version CurrentVersion { get; private set; }
		public IInstalledExtension Extension { get; private set; }
		public ICommand ShowVSExtensionManagerCommand { get; private set; }

		public void ShowVSExtensionManager(object parameter)
		{
			MenuCommandService menuCommandService = serviceProvider.Get<MenuCommandService>();
			CommandID commandId = new CommandID(VSConstants.VsStd2010, 3000);
			menuCommandService.GlobalInvoke(commandId, parameter);
			serviceProvider.Get<ITeamExplorer>().HideNotification(GuidList.UpdateNotificationId);
		}
		
		public Task BeginUpdateCheckAsync()
		{
			return Task.Run(() =>
			{
				Initialize();
				Check.TryCatch<Exception>(BeginUpdateCheck);
			});
		}
	
		private void BeginUpdateCheck()
		{
			var updateBehavior = settingsService.Get(SettingsKeys.AutoUpdateKey, AutoUpdateBehavior.DownloadAndInstall);
			if (Extension != null && updateBehavior != AutoUpdateBehavior.None)
			{
				IInstallableExtension update;
				bool updateAvailable = ExtensionUpdater.CheckForUpdate(Extension, out update);

				if (updateAvailable && update != null)				
					OnNewUpdateFound(update);				
			}
		}

		private void Initialize()
		{
			if (extensionManager == null || Extension == null)
			{
				extensionManager = serviceProvider.Get<IVsExtensionManager>();
				var checkoutAndBuild2Package = serviceProvider.Get<CheckoutAndBuild2Package>();
				CurrentVersion = checkoutAndBuild2Package.GetType().Assembly.GetName().Version;
				Extension = GetExtension();				
				ShowVSExtensionManagerCommand = new DelegateCommand<object>(ShowVSExtensionManager);
			}
		}

		private void OnNewUpdateFound(IInstallableExtension update)
		{
			var updateBehavior = settingsService.Get(SettingsKeys.AutoUpdateKey, AutoUpdateBehavior.DownloadAndInstall);
			if (updateBehavior == AutoUpdateBehavior.NotificationOnly || (update == null && updateBehavior == AutoUpdateBehavior.DownloadAndInstall))
			{ 
				string text = string.Format("There is a new Version of {0} available! [Click here to update](2)", Extension.Header.Name); // (2) is the command parameter
				var teamExplorer = serviceProvider.Get<ITeamExplorer>();
				teamExplorer.ShowNotification(text, NotificationType.Information, NotificationFlags.RequiresConfirmation, ShowVSExtensionManagerCommand, GuidList.UpdateNotificationId);
			}
			else if (updateBehavior == AutoUpdateBehavior.DownloadAndInstall && update != null)
			{
				ExtensionUpdater.UpdateExtension(Extension, update);
			}	
		}

		private IInstalledExtension GetExtension()
		{			
			IInstalledExtension extension;
			return extensionManager.TryGetInstalledExtension(GuidList.guidCheckoutAndBuild2PkgString, out extension) ? extension : null;
		}
	}
}