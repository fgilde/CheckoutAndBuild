using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class ProfileSelectorViewModel : BaseViewModel
	{
		private WorkingProfile selectedProfile;
		private ObservableCollection<WorkingProfile> profiles;
		private bool isEnabled = true;
		private bool canDeleteSelectedProfile;

		public DelegateCommand<WorkingProfile> AddProfileCommand;
		public DelegateCommand<WorkingProfile> RemoveProfileCommand;

		public ProfileSelectorViewModel(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
			AddProfileCommand = new DelegateCommand<WorkingProfile>(AddNewProfile);
			RemoveProfileCommand = new DelegateCommand<WorkingProfile>(DeleteProfile, CanExecuteDeleteSelectedProfile);
			Profiles = new ObservableCollection<WorkingProfile>();
			LoadProfiles();
			TryLoadLastProfile();
		}
		
		public bool CanDeleteSelectedProfile
		{
			get { return canDeleteSelectedProfile; }
			set { SetProperty(ref canDeleteSelectedProfile, value); }
		}

		private bool CanExecuteDeleteSelectedProfile(WorkingProfile arg)
		{
			return arg != null && !arg.IsDefault;
		}

		public bool HasProfiles
		{
			get { return Profiles != null && Profiles.Any(profile => !profile.IsDefault); }
		}

		public void DeleteProfile(WorkingProfile profile)
		{
			if (profile != null && !profile.IsDefault)
			{
				var toDelete = profile;
				IsEnabled = false;
				var nameEdit = new NameEdit((ne, value) =>
				{
					Profiles.Remove(toDelete);
					WorkingProfile.SaveProfiles(Profiles);
					SelectedProfile = Profiles.FirstOrDefault();
					IsEnabled = true;
					RaisePropertyChanged(() => HasProfiles);
				}, ne => IsEnabled = true)
				{
					IsReadOnly = true,
					Value = $"Profile '{SelectedProfile.Name}' will be deleted!",
					AcceptText = "Delete"
				};
				GetService<MainViewModel>().ExtraContent = nameEdit;				
			}
		}

		private void AddNewProfile(WorkingProfile workingProfile)
		{
			IsEnabled = false;
			var nameEdit = new NameEdit((ne,value) =>
			{                
                AddNewProfile(value, ne.IsChecked);
				IsEnabled = true;
			}, ne => IsEnabled = true)
			{
				Watermark = "Enter a profile name <Required>",
                CheckboxText = "Copy Settings from current Profile",
                HasCheckbox = true,
				AcceptText = "Add"
			};
			GetService<MainViewModel>().ExtraContent = nameEdit;
		}

		public async void AddNewProfile(string name, bool copySettingsFromCurrent)
		{
			var newProfile = WorkingProfile.CreateProfile(name);
			Profiles.Add(newProfile);
			WorkingProfile.SaveProfiles(Profiles);
		    await CopySettingsFromCurrentProfileTo(newProfile);
            SelectedProfile = newProfile;            
			RaisePropertyChanged(() => HasProfiles);
		}

	    private Task CopySettingsFromCurrentProfileTo(WorkingProfile newProfile)
	    {            
	        return Task.Run(() =>
	        {
	            var settingsService = Settings;
	            foreach (var workspace in TfsContext.GetWorkspaces())	            
	                settingsService.CopySettings(SelectedProfile, workspace, newProfile, workspace);	            
	        });
	    }

	    public ObservableCollection<WorkingProfile> Profiles
		{
			get { return profiles; }
			set { SetProperty(ref profiles, value); }
		}



		public WorkingProfile SelectedProfile
		{
			get { return selectedProfile; }
			set
			{
				//TfsContext.SelectedWorkspace = value;
				if (SetProperty(ref selectedProfile, value))
				{
					if(TfsContext != null)
						TfsContext.SelectedProfile = value;
					Settings.Set(SettingsKeys.LastProfileKey, SelectedProfile?.Id ?? Guid.Empty);
					RemoveProfileCommand.RaiseCanExecuteChanged();
					AddProfileCommand.RaiseCanExecuteChanged();
					CanDeleteSelectedProfile = SelectedProfile != null && !SelectedProfile.IsDefault;
				}
			}
		}
		

		public bool IsEnabled
		{
			get { return isEnabled; }
			set { SetProperty(ref isEnabled, value); }
		}
		
		protected override void OnUpdateSynchronous(CancellationToken cancellationToken)
		{
		    if (!cancellationToken.IsCancellationRequested)
		    {
		        LoadProfiles();
		        TryLoadLastProfile();
		    }
		}

		protected override void OnUpdateAsynchronous(CancellationToken cancellationToken)
		{
		    if (!cancellationToken.IsCancellationRequested)
		    {
		        base.OnUpdateAsynchronous(cancellationToken);
		    }
		}


		private void LoadProfiles()
		{
			Profiles.Clear();
			Profiles.AddRange(WorkingProfile.LoadProfiles());
			RaisePropertyChanged(() => HasProfiles);
		}
	
		private void TryLoadLastProfile()
		{
			if (Settings != null)
			{
				var profileId = Settings.Get(SettingsKeys.LastProfileKey, Guid.Empty);
				SelectedProfile = Profiles.FirstOrDefault(w => w.Id == profileId) ?? Profiles.FirstOrDefault();
			}
		}

	}
}