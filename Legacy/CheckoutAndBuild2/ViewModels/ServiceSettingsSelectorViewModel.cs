using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;
using Microsoft.Build.Framework;
using Microsoft.Win32;


namespace FG.CheckoutAndBuild2.ViewModels
{
	public class ServiceSettingsSelectorViewModel : BaseViewModel, IServiceSettings
	{
		#region Private Backing fields
		private string buildPropertiesCaption = "Build Properties";
		private readonly SettingsService settingsService;
		private bool isInPropertyEdit;
		private bool isInTargetEdit;
		private bool popUpEventsRegistered;
		private bool canOpenPopup = true;
		private ObservableCollection<BindablePair<string, string>> globalBuildProperties;
		private ObservableCollection<Bindable<string>> globalBuildTargets;
		private ObservableCollection<UIElement> externalSettings;
		private ObservableCollection<CheckBox> serviceSelectors;
		private string buildTargetsCaption;
		private IOperationService selectedService;

		#endregion

		#region UI-Properties

		public ObservableCollection<IOperationService> AvailableServices { get; }


		public IOperationService SelectedService
		{
			get { return selectedService; }
			set
			{
				if (SetProperty(ref selectedService, value))
					LoadExternalSettings();
			}
		}

		public string BuildPropertiesCaption
		{
			get { return buildPropertiesCaption; }
			set { SetProperty(ref buildPropertiesCaption, value); }
		}


		public string BuildTargetsCaption
		{
			get { return buildTargetsCaption; }
			set { SetProperty(ref buildTargetsCaption, value); }
		}

		public IEnumerable<LogLevel> LogLevels => Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>();

	    LoggerVerbosity IServiceSettings.LogLevel => LogLevel.ToLoggerVerbosity();

	    public LogLevel LogLevel
		{
			get { return settingsService.Get(SettingsKeys.LogLevelKey, LogLevel.Quiet); }
			set
			{
				settingsService.Set(SettingsKeys.LogLevelKey, value);
				Output.LogLevel = value;
				RaisePropertyChanged();
			}
		}

		public bool RunPreScriptsAsync
		{
			get { return settingsService.Get(SettingsKeys.RunPreScriptsAsyncKey, false); }
			set
			{
				settingsService.Set(SettingsKeys.RunPreScriptsAsyncKey, value);
				RaisePropertyChanged();
			}
		}
		
		public bool RunPostScriptsAsync
		{
			get { return settingsService.Get(SettingsKeys.RunPostScriptsAsyncKey, false); }
			set
			{
				settingsService.Set(SettingsKeys.RunPostScriptsAsyncKey, value);
				RaisePropertyChanged();
			}
		}

		public string PreBuildScriptPath
		{
			get { return settingsService.Get(SettingsKeys.PreBuildScriptPathKey, string.Empty); }
			set
			{
				settingsService.Set(SettingsKeys.PreBuildScriptPathKey, value);
				RaisePropertyChanged();                
			}
		}

		public string DelphiPath
		{
			get { return settingsService.Get(SettingsKeys.DelphiPathKey, TryFindDelphi()); }
			set
			{
				settingsService.Set(SettingsKeys.DelphiPathKey, value);
				RaisePropertyChanged();                
			}
		}

		public string PostBuildScriptPath
		{
			get { return settingsService.Get(SettingsKeys.PostBuildScriptPathKey, string.Empty); }
			set
			{
				settingsService.Set(SettingsKeys.PostBuildScriptPathKey, value);
				RaisePropertyChanged();                
			}
		}

		public IDictionary<string, string> BuildProperties
		{
			get
			{
				Dictionary<string, string> buildProperties = new Dictionary<string, string>();
				var buildProps = settingsService.Get(SettingsKeys.GlobalBuildPropertiesKey, LocalBuildService.GetDefaultBuildProperties());
				foreach (var p in buildProps)
				{
					if (!buildProperties.ContainsKey(p.Key))
						buildProperties.Add(p.Key, p.Value);
				}
				
				return buildProperties;
			}
		}

	    public T GetSettingsFromProvider<T>() where T : ISettingsProviderClass, new()
		{
			return settingsService.GetSettingsFromProvider<T>();
		}

		public T GetSettingsFromProvider<T>(ISolutionProjectModel solutionProject) where T : ISettingsProviderClass, new()
		{
			return settingsService.GetSettingsFromProvider<T>(solutionProject);
		}

		public ObservableCollection<BindablePair<string, string>> GlobalBuildProperties
		{
			get { return globalBuildProperties; }
			set { SetProperty(ref globalBuildProperties, value); }
		}

		public ObservableCollection<Bindable<string>> GlobalBuildTargets
		{
			get { return globalBuildTargets; }
			set { SetProperty(ref globalBuildTargets, value); }
		}


		public bool IsInTargetEdit
		{
			get { return isInTargetEdit; }
			set
			{
				if (value)
					LoadBuildTargets();
				else
					SaveBuildTargets();
				SetProperty(ref isInTargetEdit, value);
			}
		}

		public bool IsInPropertyEdit
		{
			get { return isInPropertyEdit; }
			set
			{
				if (value)
					LoadBuildProperties();
				else
					SaveBuildProperties();
				SetProperty(ref isInPropertyEdit, value);
			}
		}
	    

		public ObservableCollection<UIElement> ExternalSettings
		{
			get { return externalSettings; }
			set { SetProperty(ref externalSettings, value); }
		}	

		public ObservableCollection<CheckBox> ServiceSelectors
		{
			get { return serviceSelectors; }
			set { SetProperty(ref serviceSelectors, value); }
		}


		#endregion

		public IUICommand OpenPopupCommand { get; set; }
		public IUICommand ShowBuildPropertiesCommand { get; set; }
		public IUICommand ShowBuildTargetsCommand { get; set; }
		public IUICommand SelectDelphiCommand { get; set; }
		public VersionSpecSelectorViewModel VersionSpecSelectorViewModel { get; private set; }
		
		public ServiceSettingsSelectorViewModel(IServiceProvider serviceProvider) 
			: base(serviceProvider)
		{
			AvailableServices = new ObservableCollection<IOperationService>(CheckoutAndBuild2Package.GetExportedValues<IOperationService>().OrderBy(service => service.Order));
			SelectedService = AvailableServices.FirstOrDefault();
			settingsService = serviceProvider.Get<SettingsService>();
			OpenPopupCommand = new DelegateCommand<Popup>("Options",  OnExecuteOpenPopup, CanTogglePopup);
			ShowBuildPropertiesCommand = new DelegateCommand<Popup>("Build properties", OnExecuteShowBuildProperties);
			ShowBuildTargetsCommand = new DelegateCommand<Popup>("Build Targets", OnExecuteShowBuildTargets);
			SelectDelphiCommand = new DelegateCommand<Visual>(SelectDelphi);
			TfsContext.SelectedWorkspaceChanged += (sender, args) => RaiseAllPropertiesChanged();	        
			VersionSpecSelectorViewModel = new VersionSpecSelectorViewModel(serviceProvider);
		}



		internal void LoadUIControls()
		{
			if (ServiceSelectors != null && ServiceSelectors.Any())
				ServiceSelectors.Apply(box => { box.Checked -= ServiceBoxOnCheckChanged; box.Unchecked -= ServiceBoxOnCheckChanged; });
			ServiceSelectors = new ObservableCollection<CheckBox>(AvailableServices.OrderBy(service => service.Order).Select(service => service.GetServiceSelector()));
			ServiceSelectors.Apply(box => { box.Checked += ServiceBoxOnCheckChanged; box.Unchecked += ServiceBoxOnCheckChanged; });

			LoadExternalSettings();			
			LoadBuildProperties();
			LoadBuildTargets();
		}

		#region Private Methods

		private void SelectDelphi(Visual obj)
		{
			using (new PopupStaysOpenScope(obj.FindAncestor<Popup>()))
			{
				OpenFileDialog openFileDialog = new OpenFileDialog { CheckFileExists = true, FileName = "bds.exe", InitialDirectory = Path.GetDirectoryName(DelphiPath), Filter = "Delphi Executable|bds.exe" };
				if (openFileDialog.ShowDialog() ?? true)
					DelphiPath = (openFileDialog.FileName);
			}
		}

		private void LoadBuildProperties()
		{
			var buildProps = settingsService.Get(SettingsKeys.GlobalBuildPropertiesKey, LocalBuildService.GetDefaultBuildProperties());
			GlobalBuildProperties = new ObservableCollection<BindablePair<string, string>>();
			foreach (var pair in buildProps)
				GlobalBuildProperties.Add(new BindablePair<string, string>(pair.Key, pair.Value));
			BuildPropertiesCaption = $"Build Properties ({GlobalBuildProperties.Count})";
		}

		private void SaveBuildProperties()
		{
			settingsService.Set(SettingsKeys.GlobalBuildPropertiesKey, GlobalBuildProperties.ToList()); // Speichern
			BuildPropertiesCaption = $"Build Properties ({GlobalBuildProperties.Count})";
		}

		private void LoadBuildTargets()
		{
			var buildProps = settingsService.Get(SettingsKeys.GlobalBuildTargetsKey, new List<string> { "Clean", "Build" });
			GlobalBuildTargets = new ObservableCollection<Bindable<string>>(buildProps.Select(s => new Bindable<string>(s)));
			BuildTargetsCaption = $"Build Targets ({GlobalBuildTargets.Count})";
		}

		private void SaveBuildTargets()
		{
			settingsService.Set(SettingsKeys.GlobalBuildTargetsKey, GlobalBuildTargets.Select(bindable => bindable.Value).ToList()); // Speichern
			BuildTargetsCaption = $"Build Targets ({GlobalBuildTargets.Count})";
			serviceProvider.Get<MainViewModel>().IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects).Apply(model => model.RaisePropertiesChanged(() => model.BuildTargetsCaption, () => model.BuildTargets));
		}

		private void LoadExternalSettings()
		{
			ExternalSettings = new ObservableCollection<UIElement>(CheckoutAndBuild2Package.GetExportedValues<ISettingsProviderClass>()
					.SelectMany(sp => sp.GetUIElements(SelectedService, SettingsAvailability.Global, SettingsAvailability.GlobalWithProjectSpecificOverride)).OrderBy(element => element.GetType().ToString()));            
		}

		private void ServiceBoxOnCheckChanged(object sender, RoutedEventArgs e)
		{
			serviceProvider.Get<MainViewModel>()
				.IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects)
				.Apply(model => model.RaisePropertiesChanged(() => model.ServicesCaption, () => model.ServicesCaptionSmall));
		}


		private bool CanTogglePopup(Popup popup)
		{
			var res = canOpenPopup;
			if (!canOpenPopup && !popup.IsOpen)
			{
				canOpenPopup = true;
				OpenPopupCommand.RaiseCanExecuteChanged();
			}
			return res;
		}

		private void RegisterPopUpEvents(Popup popup)
		{
			if (!popUpEventsRegistered && popup != null)
			{
				popup.Closed += (sender, args) =>
				{
					Task.Delay(300).ContinueWith(task =>
					{
						canOpenPopup = true;
						OpenPopupCommand.RaiseCanExecuteChanged();
					}, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
				};
				popUpEventsRegistered = true;
			}
		}

		private void OnExecuteOpenPopup(Popup popup)
		{
			RegisterPopUpEvents(popup);
			popup.IsOpen = !popup.IsOpen;
			if (popup.IsOpen)
			{
				LoadUIControls();
				canOpenPopup = false;
			}
			OpenPopupCommand.RaiseCanExecuteChanged();
		}

		private string TryFindDelphi()
		{
			var res = Environment.GetEnvironmentVariable("BDS");
			if (!string.IsNullOrEmpty(res))
				res = Path.Combine(res, "bin", "bds.exe");
			if (File.Exists(res))
				return res;
			return @"C:\Program Files\Embarcadero\RAD Studio\9.0\bin\bds.exe";
		}

		private void OnExecuteShowBuildProperties(Popup popup)
		{
			IsInPropertyEdit = !IsInPropertyEdit;
		}

		private void OnExecuteShowBuildTargets(Popup popup)
		{
			IsInTargetEdit = !IsInTargetEdit;
		}

		#endregion

	}
}
