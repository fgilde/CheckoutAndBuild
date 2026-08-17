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

		public string RepositoryPath { get; set; }

		public string Branch { get; set; }
	}
}
