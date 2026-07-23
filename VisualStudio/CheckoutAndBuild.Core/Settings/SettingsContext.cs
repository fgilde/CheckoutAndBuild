namespace CheckoutAndBuild.Core.Settings
{
	/// <summary>
	/// Scoping dimensions for a settings lookup: profile, repository and (optionally) branch.
	/// Replaces the old TFS scoping (server/team project/workspace) from
	/// FG.CheckoutAndBuild2.Services.SettingsService.PrepareKey.
	/// </summary>
	public sealed class SettingsContext
	{
		public const string DefaultProfile = "Default";

		public string Profile { get; set; } = DefaultProfile;

		/// <summary>Repository the setting applies to; null means global.</summary>
		public string RepositoryPath { get; set; }

		/// <summary>Branch the setting applies to; null means repo-scoped.</summary>
		public string Branch { get; set; }
	}
}
