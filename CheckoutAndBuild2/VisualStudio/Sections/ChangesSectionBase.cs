using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	/// <summary>
	/// Changes section base class.
	/// </summary>
	public abstract class ChangesSectionBase : TeamExplorerBase
	{

		private string sectionTitle;
		private ChangesSectionView view;
		private bool isInUserInfoPage;
		private int displayCount;
		private int itemCount;

		public string QueryPath { get; set; }

		public string SectionTitle
		{
			get { return sectionTitle; }
			set
			{
				SetProperty(ref sectionTitle, value);
				RaisePropertyChanged(()=>Title);
			}
		}

		public bool IsInUserInfoPage
		{
			get { return isInUserInfoPage; }
			set
			{
				if(SetProperty(ref isInUserInfoPage, value))
					RaisePropertyChanged(() => IsUserNameVisible);
			}
		}

		public virtual bool IsUserNameVisible { get { return !IsInUserInfoPage; } } 

		/// <summary>
		/// Get the title of this page. If the title changes, the PropertyChanged event should be raised.
		/// </summary>
		/// <returns>
		/// Returns <see cref="T:System.String"/>.
		/// </returns>
		public override string Title
		{
			get { return SectionTitle; }
		}

		public Visibility SliderVisibility
		{
			get { return Changesets == null || Changesets.Count < 50 ? Visibility.Collapsed : Visibility.Visible; }
		}


		public int ItemCount
		{
			get { return itemCount; }
			set { SetProperty(ref itemCount, value); }
		}

		public int DisplayCount
		{
			get { return displayCount; }
			set
			{
				SetProperty(ref displayCount, value);
				RaisePropertyChanged(() => Changesets);
			}
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		protected ChangesSectionBase()
		{           
			View.ParentSection = this;
		}

		public override object Content
		{
			get { return View; }
		}

		/// <summary>
		/// Get the view.
		/// </summary>
		protected ChangesSectionView View
		{
			get { return view ?? (view = new ChangesSectionView());}
		}

		/// <summary>
		/// Store the base title without any decorations for later use.
		/// </summary>
		private string BaseTitle { get; set; }

		/// <summary>
		/// Initialize override.
		/// </summary>
		public async override void Initialize(object sender, IServiceProvider provider, object context)
		{
			base.Initialize(sender, provider, context);
			TeamExplorer.PropertyChanged -= TeamExplorerOnPropertyChanged;
			TeamExplorer.PropertyChanged += TeamExplorerOnPropertyChanged;

			BaseTitle = Title;

			if (context is ChangesSectionContext)
			{
				ChangesSectionContext ctx = (ChangesSectionContext)context;
				Changesets = ctx.Changesets;
				View.SelectedIndex = ctx.SelectedIndex;
			}
			else
			{
				// Kick off the initial refresh
				await RefreshAsync();
			}
		}

		/// <summary>
		/// Save contextual information about the current section state.
		/// </summary>
		public override void SaveContext(object sender, SectionSaveContextEventArgs e)
		{
			base.SaveContext(sender, e);

			ChangesSectionContext context = new ChangesSectionContext
			{
				Changesets = Changesets,
				SelectedIndex = View.SelectedIndex
			};

			e.Context = context;
		}

		/// <summary>
		/// Refresh override.
		/// </summary>
		public async override void Refresh()
		{
			base.Refresh();
			await RefreshAsync();
		}


		private void TeamExplorerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (TeamExplorer != null && e.PropertyName == CoreExtensions.GetMemberName(() => TeamExplorer.CurrentPage))
			{
				IsInUserInfoPage = TeamExplorer.CurrentPage != null && TeamExplorer.CurrentPage.GetType() == typeof(UserInfoPage);
			}
		}

		/// <summary>
		/// Refresh the changeset data asynchronously.
		/// </summary>
		private async Task RefreshAsync()
		{
			if (!IsEnabled)
			{
				IsBusy = false;
				return;
			}
			try
			{
				IsBusy = true;
				Changesets.Clear();

				ObservableCollection<ChangesetInfo> changesets = new ObservableCollection<ChangesetInfo>();							
				await Task.Run(() =>
				{
					ITeamFoundationContext context = CurrentContext;
					if (context != null && context.HasCollection && context.HasTeamProject)
					{
						VersionControlServer vcs = context.TeamProjectCollection.GetService<VersionControlServer>();						
						if (vcs != null)
						{
							// Ask the derived section for the history parameters
							string user;
							int maxCount;
							GetHistoryParameters(vcs, out user, out maxCount);

							string path = GetPath();
							foreach (Changeset changeset in vcs.QueryHistory(path, VersionSpec.Latest, 0, RecursionType.Full,
																			 user, null, null, maxCount, false, true))
							{
								changesets.Add(new ChangesetInfo(changeset, IsUserNameVisible ? null : string.Empty));
							}
						}
					}
				});

				Changesets = changesets;
				SectionTitle = Changesets.Count > 0 ? String.Format("{0} ({1})", BaseTitle, Changesets.Count)
													   : BaseTitle;
			}
			catch (Exception ex)
			{
				Output.Exception(ex);
			}
			finally
			{				
				IsBusy = false;
			}
		}

		/// <summary>
		/// Get the parameters for the history query.
		/// </summary>
		protected abstract void GetHistoryParameters(VersionControlServer vcs, out string user, out int maxCount);

		/// <summary>
		/// ContextChanged override.
		/// </summary>
		protected override async void ContextChanged(object sender, ContextChangedEventArgs e)
		{
			base.ContextChanged(sender, e);
					
			if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
			{
				await RefreshAsync();
			}		
		}

		public bool HasItems
		{
			get { return Changesets != null && Changesets.Any(); }
		}

		/// <summary>
		/// List of changesets.
		/// </summary>
		public ObservableCollection<ChangesetInfo> Changesets
		{
			get
			{
				if(DisplayCount == 0 || DisplayCount >= m_changesets.Count)
					return m_changesets;
				return m_changesets.Take(DisplayCount).ToObservableCollection();
			}
			protected set {
				m_changesets = value; 
				RaisePropertyChanged();
				RaisePropertyChanged(() => SliderVisibility);
				RaisePropertyChanged(() => HasItems);
				DisplayCount = ItemCount = value.Count;				
			}
		}

		private ObservableCollection<ChangesetInfo> m_changesets = new ObservableCollection<ChangesetInfo>();

		/// <summary>
		/// Class to preserve the contextual information for this section.
		/// </summary>
		private class ChangesSectionContext
		{
			public ObservableCollection<ChangesetInfo> Changesets { get; set; }
			public int SelectedIndex { get; set; }
		}

		/// <summary>
		/// View details for the changeset.
		/// </summary>
		public void ViewChangesetDetails(int changesetId)
		{

			ITeamExplorer teamExplorer = GetService<ITeamExplorer>();
			if (teamExplorer != null)
				teamExplorer.NavigateToPage(new Guid(TeamExplorerPageIds.ChangesetDetails), changesetId);

			//var vce = CheckoutAndBuild2Package.GetGlobalService<VersionControlExt>();
			//vce.ViewChangesetDetails(changesetId);
		}

		/// <summary>
		/// View history using the same parameters as this section.
		/// </summary>
		public void ViewHistory()
		{
			ITeamFoundationContext context = CurrentContext;
			if (context != null && context.HasCollection && context.HasTeamProject)
			{
				VersionControlServer vcs = context.TeamProjectCollection.GetService<VersionControlServer>();
				if (vcs != null)
				{
					// Ask the derived section for the history parameters
					string path = GetPath();
					string user;
					int maxCount;
					GetHistoryParameters(vcs, out user, out maxCount);

					VersionControlExt vcExt = serviceProvider.Get<VersionControlExt>();
					if (vcExt != null)
					{
						vcExt.History.Show(path, VersionSpec.Latest, 0, RecursionType.Full,
										   user, null, null, Int32.MaxValue, true);
					}
				}
			}
		}

		private string GetPath()
		{
			if(!string.IsNullOrEmpty(QueryPath))
				return QueryPath;
			ITeamFoundationContext context = CurrentContext;
			if (context != null && context.HasCollection && context.HasTeamProject)
			{
				return "$/" + context.TeamProjectName;
			}
			return "$/";
		}

	}
}
