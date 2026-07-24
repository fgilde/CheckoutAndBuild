using System;
using System.ComponentModel;

namespace CheckoutAndBuild2.Contracts.Settings
{
	public class SettingsPropertyAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Attribute"/> class.
		/// </summary>
		public SettingsPropertyAttribute(SettingsAvailability availability, string name, string description = "")
		{
            Availability = availability;
			Name = Description = name;			
			if(!string.IsNullOrEmpty(description))
				Description = description;
		}

		public string ServiceId { get; set; }
		public SettingsAvailability Availability { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
	}
}