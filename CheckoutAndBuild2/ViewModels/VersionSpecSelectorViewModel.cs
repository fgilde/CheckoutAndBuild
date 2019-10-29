using System;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class VersionSpecSelectorViewModel : BaseViewModel
	{
		private readonly SettingsService settingsService;

		public VersionSpecSelectorViewModel(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{			
			settingsService = serviceProvider.Get<SettingsService>();
			ChangeVersionCommand = new DelegateCommand<VersionSpec>(ChangeVersionSpec);			
			this.OnChange(() => HasWorkspaceOrDirectory, b => RaisePropertiesChanged(() => VersionSpec));
		}

		private void ChangeVersionSpec(VersionSpec o)
		{
			var res = TeamControlFactory.ShowDialogChooseVersion(o);
			if (res != null && res != o)
				VersionSpec = res;
		}
		
		public ICommand ChangeVersionCommand { get; private set; }

		public VersionSpec VersionSpec
		{
			get { return settingsService.Get(SettingsKeys.VersionSpecKey, VersionSpec.Latest); }
			set
			{
				settingsService.Set(SettingsKeys.VersionSpecKey, value);
				RaisePropertyChanged();				
			}
		}
	}
}