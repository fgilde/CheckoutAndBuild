using System;
using System.Collections.Generic;
using System.Linq;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Services;

namespace FG.CheckoutAndBuild2.Types
{
	public class WorkingProfile : NotificationObject
	{
		private string name;
		
		public Guid Id { get;  set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="NotificationObject"/> class.
		/// </summary>
		private WorkingProfile(string name, Guid id)
		{
			this.name = name;
			Id = id;
		}

		[Obsolete("For serialize only")]
		public WorkingProfile()
		{}


		public bool IsDefault => Id == Guid.Empty;

	    public string Name
		{
			get { return name; }
			set { SetProperty(ref name, value); }
		}

		public override string ToString()
		{
			return Name;
		}

		public static WorkingProfile DefaultProfile => new WorkingProfile("Default", Guid.Empty);

	    public static WorkingProfile CreateProfile(string name, bool save = false)
		{
			var res = new WorkingProfile(name, Guid.NewGuid());
		    if (save)
		    {		        
		        SaveProfiles(new List<WorkingProfile>(LoadProfiles()) { res });
		    }
		    return res;
		}

		public static void SaveProfiles(IEnumerable<WorkingProfile> profiles)
		{			
			CheckoutAndBuild2Package.GetGlobalService<SettingsService>().Set(SettingsKeys.AllProfilesKey, profiles.ToList());
		}

		public static WorkingProfile[] LoadProfiles()
		{
			var settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
			var profiles = settingsService.Get(SettingsKeys.AllProfilesKey, Enumerable.Empty<WorkingProfile>().ToList());
			if(!profiles.Any(profile => profile.IsDefault))
				profiles.Insert(0, DefaultProfile);						
			return profiles.ToArray();
		}
	}
}