using System;
using System.ComponentModel;
using Microsoft.TeamFoundation.Controls;

namespace CheckoutAndBuild.VisualStudio.TeamExplorer
{
	/// <summary>
	/// Team Explorer Home section hosting the CheckoutAndBuild UI (same view model as the tool window),
	/// for as long as VS still ships the classic Team Explorer.
	/// </summary>
	[TeamExplorerSection(SectionId, TeamExplorerPageIds.Home, 200)]
	public class CoabHomeSection : ITeamExplorerSection
	{
		public const string SectionId = "c9a40326-f7ac-4503-a563-d8b3b5ebcb50";

		private bool isExpanded = true;
		private bool isVisible = true;
		private object sectionContent;

		public event PropertyChangedEventHandler PropertyChanged;

		public string Title => "CheckoutAndBuild";

		public object SectionContent => sectionContent ?? (sectionContent = new CoabHomeSectionView());

		public bool IsExpanded
		{
			get => isExpanded;
			set => Set(ref isExpanded, value, nameof(IsExpanded));
		}

		public bool IsVisible
		{
			get => isVisible;
			set => Set(ref isVisible, value, nameof(IsVisible));
		}

		public bool IsBusy => false;

		// LoadAsync is kicked off by MainToolWindowControl's own Loaded handler (guarded, runs once).
		public void Initialize(object sender, SectionInitializeEventArgs e) { }
		public void Loaded(object sender, SectionLoadedEventArgs e) { }
		public void SaveContext(object sender, SectionSaveContextEventArgs e) { }
		public void Refresh() { }
		public void Cancel() { }
		public object GetExtensibilityService(Type serviceType) => null;
		public void Dispose() { }

		private void Set(ref bool field, bool value, string name)
		{
			if (field == value)
				return;
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}
