using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Git;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;
using GitChange = CheckoutAndBuild.Core.Git.GitChange;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>One git repository found beneath the configured working folders.</summary>
	public class GitRepositoryViewModel : NotificationObject
	{
		private string branch;

		public GitRepositoryViewModel(string path)
		{
			Path = path;
			Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
		}

		public string Path { get; }
		public string Name { get; }

		/// <summary>Working folder the repository was discovered beneath (group header in the repository selector).</summary>
		public string Folder { get; set; } = "";

		public string Branch
		{
			get { return branch; }
			set
			{
				if (SetProperty(ref branch, value))
					RaisePropertyChanged(nameof(DisplayText));
			}
		}

		public string DisplayText => string.IsNullOrEmpty(Branch) ? Name : $"{Name}  ({Branch})";
	}

	/// <summary>One row of the Changes tab (wraps a Core GitChange).</summary>
	public class ChangeViewModel
	{
		private static readonly Brush AddedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0x8A, 0x34)));
		private static readonly Brush ModifiedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)));
		private static readonly Brush DeletedBrush = Freeze(new SolidColorBrush(Colors.Firebrick));
		private static readonly Brush RenamedBrush = Freeze(new SolidColorBrush(Colors.DarkOrange));
		private static readonly Brush UntrackedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x99, 0x37)));
		private static readonly Brush ConflictedBrush = Freeze(new SolidColorBrush(Colors.Red));

		private static Brush Freeze(Brush brush)
		{
			brush.Freeze();
			return brush;
		}

		public ChangeViewModel(GitChange change, string repoDir)
		{
			Change = change;
			FullPath = Path.Combine(repoDir, change.FilePath.Replace('/', Path.DirectorySeparatorChar));
		}

		public GitChange Change { get; }
		public string FilePath => Change.FilePath;
		public string FullPath { get; }

		public string Group =>
			Change.ChangeType == GitChangeType.Conflicted ? "Conflicts"
			: Change.ChangeType == GitChangeType.Untracked ? "Untracked Files"
			: Change.IsStaged ? "Staged Changes"
			: "Changes";

		public int GroupOrder =>
			Change.ChangeType == GitChangeType.Conflicted ? 0
			: Change.IsStaged && Change.ChangeType != GitChangeType.Untracked ? 1
			: Change.ChangeType != GitChangeType.Untracked ? 2
			: 3;

		public string TypeCode
		{
			get
			{
				switch (Change.ChangeType)
				{
					case GitChangeType.Added: return "A";
					case GitChangeType.Modified: return "M";
					case GitChangeType.Deleted: return "D";
					case GitChangeType.Renamed: return "R";
					case GitChangeType.Untracked: return "U";
					case GitChangeType.Conflicted: return "!";
					default: return "?";
				}
			}
		}

		public Brush TypeBrush
		{
			get
			{
				switch (Change.ChangeType)
				{
					case GitChangeType.Added: return AddedBrush;
					case GitChangeType.Deleted: return DeletedBrush;
					case GitChangeType.Renamed: return RenamedBrush;
					case GitChangeType.Untracked: return UntrackedBrush;
					case GitChangeType.Conflicted: return ConflictedBrush;
					default: return ModifiedBrush;
				}
			}
		}

		public string ToolTipText => $"{FullPath}\n{Change.ChangeType}{(Change.IsStaged ? " (staged)" : "")}";
	}

	/// <summary>One row of the Stashes tab (stash index = position in "git stash list").</summary>
	public class StashViewModel
	{
		public StashViewModel(GitStash stash, int index)
		{
			Stash = stash;
			Index = index;
		}

		public GitStash Stash { get; }
		public int Index { get; }

		public string Id => Stash.Id;
		public string Name => Stash.Name;
		public string Branch => Stash.Branch;
		public string TimeInfo => Stash.TimeInfo;
		public string Creator => Stash.Creator;
	}

	/// <summary>One row of the Branches tab.</summary>
	public class BranchViewModel : NotificationObject
	{
		private string syncBadge;

		public BranchViewModel(string name, bool isCurrent)
		{
			Name = name;
			IsCurrent = isCurrent;
		}

		public string Name { get; }
		public bool IsCurrent { get; }

		public string SyncBadge
		{
			get { return syncBadge; }
			set { SetProperty(ref syncBadge, value); }
		}
	}

	/// <summary>One row of the Sync tab (one repository).</summary>
	public class RepoSyncViewModel : NotificationObject
	{
		private readonly Action<RepoSyncViewModel, string> checkoutRequested;
		private bool suppressCheckout;
		private string branch;
		private string selectedBranch;
		private string syncBadge;
		private string status;

		public RepoSyncViewModel(GitRepositoryViewModel repository, Action<RepoSyncViewModel, string> checkoutRequested = null)
		{
			Repository = repository;
			this.checkoutRequested = checkoutRequested;
		}

		public GitRepositoryViewModel Repository { get; }
		public string Name => Repository.Name;
		public string Path => Repository.Path;
		public string FolderName => Repository.Folder;
		public bool HasUpstream { get; set; }

		public ObservableCollection<string> Branches { get; } = new ObservableCollection<string>();

		/// <summary>Fills the branch dropdown without triggering a checkout.</summary>
		public void SetBranches(IEnumerable<string> branches, string current)
		{
			suppressCheckout = true;
			Branches.Clear();
			foreach (string name in branches)
				Branches.Add(name);
			SelectedBranch = current;
			suppressCheckout = false;
		}

		public string SelectedBranch
		{
			get { return selectedBranch; }
			set
			{
				if (SetProperty(ref selectedBranch, value) && !suppressCheckout && value != null && value != Branch)
					checkoutRequested?.Invoke(this, value);
			}
		}

		public string Branch
		{
			get { return branch; }
			set { SetProperty(ref branch, value); }
		}

		public string SyncBadge
		{
			get { return syncBadge; }
			set { SetProperty(ref syncBadge, value); }
		}

		public string Status
		{
			get { return status; }
			set { SetProperty(ref status, value); }
		}
	}

	/// <summary>One row of the Worktrees tab.</summary>
	public class WorktreeViewModel : NotificationObject
	{
		private string syncBadge = "…";
		private string dirtyBadge;

		public WorktreeViewModel(Core.Git.GitWorktree worktree)
		{
			Worktree = worktree;
			var badges = new List<string>();
			if (worktree.IsMain) badges.Add("main");
			if (worktree.IsDetached) badges.Add("detached");
			if (worktree.IsLocked) badges.Add(string.IsNullOrEmpty(worktree.LockReason) ? "locked" : $"locked: {worktree.LockReason}");
			if (worktree.IsPrunable) badges.Add("prunable");
			if (!worktree.Exists && !worktree.IsPrunable) badges.Add("missing");
			Badges = string.Join("  ", badges.Select(b => $"[{b}]"));
		}

		public Core.Git.GitWorktree Worktree { get; }
		public string Name => Worktree.Name;
		public string Path => Worktree.Path;
		public string BranchDisplay => Worktree.IsDetached ? "(detached)" : Worktree.Branch;
		public string ShortSha => Worktree.ShortSha;
		public string Badges { get; }
		public bool IsMain => Worktree.IsMain;

		public string SyncBadge
		{
			get { return syncBadge; }
			set { SetProperty(ref syncBadge, value); }
		}

		public string DirtyBadge
		{
			get { return dirtyBadge; }
			set { SetProperty(ref dirtyBadge, value); }
		}
	}

	/// <summary>One row of the Feed tab (commit + owning repository).</summary>
	public class FeedCommitViewModel
	{
		public FeedCommitViewModel(GitRepositoryViewModel repository, GitCommit commit)
		{
			Repository = repository;
			Commit = commit;
			DateTimeOffset date;
			SortDate = DateTimeOffset.TryParse(commit.Date, out date) ? date : DateTimeOffset.MinValue;
		}

		public GitRepositoryViewModel Repository { get; }
		public GitCommit Commit { get; }
		public DateTimeOffset SortDate { get; }

		public string RepoName => Repository.Name;
		public string ShortSha => Commit.ShortSha;
		public string Author => Commit.Author;
		public string Date => Commit.Date;
		public string Message => Commit.Message;
	}

	/// <summary>Root view model of the "CheckoutAndBuild Git" tool window.</summary>
	public class GitViewModel : NotificationObject
	{
		private const string workingFoldersKey = "WorkingFolders";
		private const int maxScanDepth = 3;
		private static readonly string[] skippedDirectories = { ".vs", "bin", "obj", "node_modules", "packages" };

		private readonly GitService git = new GitService();
		private readonly ISettingsService settings;
		private readonly SettingsContext globalContext = new SettingsContext();

		private const int historyTabIndex = 2;
		private const int syncTabIndex = 4;
		private const int feedTabIndex = 5;
		private const int worktreesTabIndex = 6;
		private static readonly int?[] periodDays = { null, 7, 30, 90 };

		private GitRepositoryViewModel selectedRepository;
		private ChangeViewModel selectedChange;
		private StashViewModel selectedStash;
		private GitCommit selectedCommit;
		private GitCommitFile selectedCommitFile;
		private BranchViewModel selectedBranch;
		private string diffText;
		private string stashDiffText;
		private string commitFileDiffText;
		private string newStashMessage;
		private string newBranchName;
		private string historyGrep;
		private string historyAuthor;
		private string historyPathFilter;
		private string commitMessage;
		private string multiRepoBranch;
		private bool createBranchIfMissing = true;
		private bool onlyMine;
		private int historyPeriodIndex;
		private bool isBusy;
		private Task loadTask;
		private string lastError;
		private string statusMessage;
		private int selectedTabIndex;

		public GitViewModel() : this(JsonSettingsService.CreateDefault())
		{
		}

		public GitViewModel(ISettingsService settingsService)
		{
			settings = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			RefreshCommand = new DelegateCommand(() => RunSafe(RefreshAllAsync), () => !IsBusy);
			ExportPatchCommand = new DelegateCommand(() => RunSafe(ExportPatchAsync), () => HasRepository && !IsBusy);
			ExportZipCommand = new DelegateCommand(() => RunSafe(ExportZipAsync), () => HasRepository && !IsBusy);
			ApplyPatchCommand = new DelegateCommand(() => RunSafe(ApplyPatchAsync), () => HasRepository && !IsBusy);
			SuggestBranchCommand = new DelegateCommand(() => RunSafe(SuggestBranchNameAsync), () => HasRepository && !IsBusy);

			CompareChangeCommand = new DelegateCommand(p => RunSafe(() => CompareChangeAsync(p as ChangeViewModel)),
				p => p is ChangeViewModel c && c.Change.ChangeType != Core.Git.GitChangeType.Untracked && c.Change.ChangeType != Core.Git.GitChangeType.Added && !IsBusy);
			OpenChangeCommand = new DelegateCommand(p => OpenChange(p as ChangeViewModel), p => (p as ChangeViewModel)?.FullPath != null && System.IO.File.Exists(((ChangeViewModel)p).FullPath));
			OpenChangeFolderCommand = new DelegateCommand(
				p => { if (p is ChangeViewModel c) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{c.FullPath}\""); },
				p => p is ChangeViewModel);
			CopyChangePathCommand = new DelegateCommand(
				p => { if (p is ChangeViewModel c) Clipboard.SetText(c.FullPath); }, p => p is ChangeViewModel);
			FileHistoryCommand = new DelegateCommand(p => RunSafe(() => ShowFileHistoryAsync(p as ChangeViewModel)), p => p is ChangeViewModel);
			StageChangeCommand = new DelegateCommand(p => RunSafe(() => StageChangeAsync(p as ChangeViewModel, stage: true)),
				p => p is ChangeViewModel c && !c.Change.IsStaged && !IsBusy);
			UnstageChangeCommand = new DelegateCommand(p => RunSafe(() => StageChangeAsync(p as ChangeViewModel, stage: false)),
				p => p is ChangeViewModel c && c.Change.IsStaged && !IsBusy);
			DiscardChangeCommand = new DelegateCommand(p => DiscardChangeWithConfirm(p as ChangeViewModel),
				p => p is ChangeViewModel && !IsBusy);
			CommitAllCommand = new DelegateCommand(() => RunSafe(() => CommitAllAsync(push: false)), CanCommit);
			CommitAndPushCommand = new DelegateCommand(() => RunSafe(() => CommitAllAsync(push: true)), CanCommit);
			CommitFromWorkItemCommand = new DelegateCommand(() => RunSafe(CommitMessageFromWorkItemAsync), () => HasRepository && !IsBusy);
			ClearHistoryPathFilterCommand = new DelegateCommand(() => { HistoryPathFilter = null; RunSafe(LoadHistoryAsync); });
			RefreshWorktreesCommand = new DelegateCommand(() => RunSafe(LoadWorktreesAsync), () => HasRepository && !IsBusy);
			AddWorktreeCommand = new DelegateCommand(() => RunSafe(AddWorktreeAsync), () => HasRepository && !IsBusy);
			RemoveWorktreeCommand = new DelegateCommand(p => RemoveWorktreeWithConfirm(p as WorktreeViewModel),
				p => p is WorktreeViewModel w && !w.Worktree.IsMain && !IsBusy);
			PruneWorktreesCommand = new DelegateCommand(() => RunSafe(PruneWorktreesAsync), () => HasRepository && !IsBusy);
			OpenWorktreeExplorerCommand = new DelegateCommand(
				p => { if (p is WorktreeViewModel w && w.Worktree.Exists) System.Diagnostics.Process.Start("explorer.exe", $"\"{w.Path}\""); },
				p => (p as WorktreeViewModel)?.Worktree.Exists == true);
			OpenWorktreeSolutionCommand = new DelegateCommand(p => OpenWorktreeSolution(p as WorktreeViewModel),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true);
			AddWorktreeAsFolderCommand = new DelegateCommand(p => AddWorktreeAsWorkingFolder(p as WorktreeViewModel),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true);
			PullWorktreeCommand = new DelegateCommand(p => RunSafe(() => WorktreeActionAsync(p as WorktreeViewModel, "pull")),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true && !IsBusy);
			PushWorktreeCommand = new DelegateCommand(p => RunSafe(() => WorktreeActionAsync(p as WorktreeViewModel, "push")),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true && !IsBusy);
			UpdateWorktreeFromBaseCommand = new DelegateCommand(p => RunSafe(() => WorktreeActionAsync(p as WorktreeViewModel, "update")),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true && !IsBusy);
			SwitchWorktreeBranchCommand = new DelegateCommand(p => RunSafe(() => SwitchWorktreeBranchAsync(p as WorktreeViewModel)),
				p => (p as WorktreeViewModel)?.Worktree.Exists == true && !IsBusy);
			FindOrphanWorktreesCommand = new DelegateCommand(() => RunSafe(FindOrphanWorktreesAsync), () => HasRepository && !IsBusy);
			CheckoutAllCommand = new DelegateCommand(() => RunSafe(CheckoutAllAsync),
				() => SyncRows.Count > 0 && !string.IsNullOrWhiteSpace(MultiRepoBranch) && !IsBusy);
			CleanupBranchesCommand = new DelegateCommand(() => RunSafe(CleanupMergedBranchesAsync), () => SyncRows.Count > 0 && !IsBusy);
			CreatePullRequestCommand = new DelegateCommand(p => RunSafe(() => CreatePullRequestAsync(p as RepoSyncViewModel)), p => p is RepoSyncViewModel);
			StashPushCommand = new DelegateCommand(() => RunSafe(StashPushAsync), () => HasRepository && !IsBusy);
			StashApplyCommand = new DelegateCommand(() => RunSafe(() => StashActionAsync("apply")), () => SelectedStash != null && !IsBusy);
			StashPopCommand = new DelegateCommand(() => RunSafe(() => StashActionAsync("pop")), () => SelectedStash != null && !IsBusy);
			StashDropCommand = new DelegateCommand(() => RunSafe(StashDropAsync), () => SelectedStash != null && !IsBusy);

			CheckoutBranchCommand = new DelegateCommand(() => RunSafe(CheckoutSelectedBranchAsync), () => SelectedBranch != null && !SelectedBranch.IsCurrent && !IsBusy);
			CreateBranchCommand = new DelegateCommand(() => RunSafe(CreateBranchAsync), () => HasRepository && !string.IsNullOrWhiteSpace(NewBranchName) && !IsBusy);
			DeleteBranchCommand = new DelegateCommand(() => RunSafe(DeleteBranchAsync), () => SelectedBranch != null && !SelectedBranch.IsCurrent && !IsBusy);
			RefreshBranchesCommand = new DelegateCommand(() => RunSafe(LoadBranchesAsync), () => HasRepository && !IsBusy);

			ForcePushRepoCommand = new DelegateCommand(p => ForcePushWithConfirm((RepoSyncViewModel)p), p => !IsBusy);
			FetchRepoCommand = new DelegateCommand(p => RunSafe(() => SyncRowActionAsync((RepoSyncViewModel)p, "fetch")), p => !IsBusy);
			PullRepoCommand = new DelegateCommand(p => RunSafe(() => SyncRowActionAsync((RepoSyncViewModel)p, "pull")), p => !IsBusy);
			PushRepoCommand = new DelegateCommand(p => RunSafe(() => SyncRowActionAsync((RepoSyncViewModel)p, "push")), p => !IsBusy);
			FetchAllCommand = new DelegateCommand(() => RunSafe(() => SyncAllAsync("fetch")), () => SyncRows.Count > 0 && !IsBusy);
			PullAllCommand = new DelegateCommand(() => RunSafe(() => SyncAllAsync("pull")), () => SyncRows.Count > 0 && !IsBusy);
			PushAllCommand = new DelegateCommand(() => RunSafe(() => SyncAllAsync("push")), () => SyncRows.Count > 0 && !IsBusy);
			RefreshSyncCommand = new DelegateCommand(() => RunSafe(RefreshSyncAsync), () => !IsBusy);

			RefreshHistoryCommand = new DelegateCommand(() => RunSafe(LoadHistoryAsync), () => HasRepository && !IsBusy);
			CopyShaCommand = new DelegateCommand(() => { if (SelectedCommit != null) Clipboard.SetText(SelectedCommit.Sha); }, () => SelectedCommit != null);
			RefreshFeedCommand = new DelegateCommand(() => RunSafe(RefreshFeedAsync), () => Repositories.Count > 0 && !IsBusy);
		}

		public ObservableCollection<GitRepositoryViewModel> Repositories { get; } = new ObservableCollection<GitRepositoryViewModel>();
		public ObservableCollection<ChangeViewModel> Changes { get; } = new ObservableCollection<ChangeViewModel>();
		public ObservableCollection<StashViewModel> Stashes { get; } = new ObservableCollection<StashViewModel>();
		public ObservableCollection<GitCommit> Commits { get; } = new ObservableCollection<GitCommit>();
		public ObservableCollection<GitCommitFile> CommitFiles { get; } = new ObservableCollection<GitCommitFile>();
		public ObservableCollection<BranchViewModel> Branches { get; } = new ObservableCollection<BranchViewModel>();
		public ObservableCollection<RepoSyncViewModel> SyncRows { get; } = new ObservableCollection<RepoSyncViewModel>();
		public ObservableCollection<FeedCommitViewModel> FeedCommits { get; } = new ObservableCollection<FeedCommitViewModel>();

		public ICommand RefreshCommand { get; }
		public ICommand ExportPatchCommand { get; }
		public ICommand ExportZipCommand { get; }
		public ICommand ApplyPatchCommand { get; }
		public ICommand SuggestBranchCommand { get; }
		public ICommand ForcePushRepoCommand { get; }
		public ICommand CompareChangeCommand { get; }
		public ICommand OpenChangeCommand { get; }
		public ICommand OpenChangeFolderCommand { get; }
		public ICommand CopyChangePathCommand { get; }
		public ICommand FileHistoryCommand { get; }
		public ICommand StageChangeCommand { get; }
		public ICommand UnstageChangeCommand { get; }
		public ICommand DiscardChangeCommand { get; }
		public ICommand CommitAllCommand { get; }
		public ICommand CommitAndPushCommand { get; }
		public ICommand CommitFromWorkItemCommand { get; }
		public ICommand ClearHistoryPathFilterCommand { get; }

		public string CommitMessage
		{
			get { return commitMessage; }
			set { SetProperty(ref commitMessage, value); }
		}

		public string HistoryPathFilter
		{
			get { return historyPathFilter; }
			private set
			{
				if (SetProperty(ref historyPathFilter, value))
					RaisePropertyChanged(nameof(HasHistoryPathFilter));
			}
		}

		public bool HasHistoryPathFilter => !string.IsNullOrEmpty(historyPathFilter);

		public ICommand CheckoutAllCommand { get; }
		public ICommand CleanupBranchesCommand { get; }
		public ICommand CreatePullRequestCommand { get; }
		public ICommand RefreshWorktreesCommand { get; }
		public ICommand AddWorktreeCommand { get; }
		public ICommand RemoveWorktreeCommand { get; }
		public ICommand PruneWorktreesCommand { get; }
		public ICommand OpenWorktreeExplorerCommand { get; }
		public ICommand OpenWorktreeSolutionCommand { get; }
		public ICommand AddWorktreeAsFolderCommand { get; }
		public ICommand PullWorktreeCommand { get; }
		public ICommand PushWorktreeCommand { get; }
		public ICommand UpdateWorktreeFromBaseCommand { get; }
		public ICommand SwitchWorktreeBranchCommand { get; }
		public ICommand FindOrphanWorktreesCommand { get; }

		public ObservableCollection<WorktreeViewModel> Worktrees { get; } = new ObservableCollection<WorktreeViewModel>();

		#region worktrees

		/// <summary>Opens the git window on the Worktrees tab with the given repository selected (branch dropdown of the main window).</summary>
		public async Task ShowWorktreesAsync(string repositoryPath)
		{
			await LoadAsync();
			var repo = Repositories.FirstOrDefault(r => string.Equals(r.Path, repositoryPath, StringComparison.OrdinalIgnoreCase));
			if (repo == null)
			{
				repo = new GitRepositoryViewModel(repositoryPath);
				Repositories.Add(repo);
			}
			SelectedTabIndex = worktreesTabIndex;
			SelectedRepository = repo;
			await GuardedAsync(LoadWorktreesAsync);
		}

		private async Task LoadWorktreesAsync()
		{
			var repo = SelectedRepository;
			Worktrees.Clear();
			if (repo == null)
				return;
			foreach (var worktree in await git.GetWorktreesAsync(repo.Path))
				Worktrees.Add(new WorktreeViewModel(worktree));
			foreach (var row in Worktrees.ToList())
			{
				if (!row.Worktree.Exists)
				{
					row.SyncBadge = "";
					continue;
				}
				try
				{
					var sync = await git.GetAheadBehindAsync(row.Path);
					var changes = await git.GetStatusAsync(row.Path);
					row.SyncBadge = !sync.HasUpstream ? "no upstream"
						: sync.Ahead == 0 && sync.Behind == 0 ? "✓"
						: ((sync.Ahead > 0 ? $"↑{sync.Ahead} " : "") + (sync.Behind > 0 ? $"↓{sync.Behind}" : "")).Trim();
					int dirty = changes.Select(c => c.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
					row.DirtyBadge = dirty == 0 ? "" : $"● {dirty}";
				}
				catch
				{
					row.SyncBadge = "";
				}
			}
		}

		/// <summary>Pull, push (auto upstream) or merge origin/&lt;default&gt; inside one worktree.</summary>
		private async Task WorktreeActionAsync(WorktreeViewModel worktree, string action)
		{
			if (worktree == null || !worktree.Worktree.Exists)
				return;
			StatusMessage = $"{action}: {worktree.Name}…";
			switch (action)
			{
				case "pull":
					await git.PullAsync(worktree.Path);
					break;
				case "push":
					var sync = await git.GetAheadBehindAsync(worktree.Path);
					await git.PushAsync(worktree.Path, setUpstream: !sync.HasUpstream);
					break;
				case "update":
					await git.UpdateFromBaseAsync(worktree.Path);
					break;
			}
			StatusMessage = $"{action} done: {worktree.Name}";
			await LoadWorktreesAsync();
		}

		private async Task SwitchWorktreeBranchAsync(WorktreeViewModel worktree)
		{
			var repo = SelectedRepository;
			if (worktree == null || repo == null || !worktree.Worktree.Exists)
				return;
			var branches = await git.GetBranchesAsync(repo.Path);
			var inUse = new HashSet<string>((await git.GetWorktreesAsync(repo.Path)).Select(w => w.Branch)
				.Where(b => !string.IsNullOrEmpty(b)), StringComparer.OrdinalIgnoreCase);

			var branchBox = new System.Windows.Controls.ComboBox
			{
				IsEditable = true,
				Margin = new Thickness(8, 2, 8, 4),
				ItemsSource = branches.Where(b => !inUse.Contains(b)).ToList()
			};
			var ok = new System.Windows.Controls.Button
			{
				Content = "Switch", Padding = new Thickness(12, 3, 12, 3),
				Margin = new Thickness(0, 8, 8, 8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true
			};
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = $"Checkout branch in worktree '{worktree.Name}' (existing or new name):",
				Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7, TextWrapping = TextWrapping.Wrap
			});
			panel.Children.Add(branchBox);
			panel.Children.Add(ok);
			var window = new Window
			{
				Title = "Switch Branch",
				Content = panel,
				Width = 420,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			window.Loaded += (s, e) => branchBox.Focus();
			if (window.ShowDialog() != true)
				return;
			string branch = branchBox.Text?.Trim();
			if (string.IsNullOrEmpty(branch))
				return;
			if (await git.BranchExistsAsync(repo.Path, branch))
				await git.CheckoutBranchAsync(worktree.Path, branch);
			else
				await git.CreateBranchAsync(worktree.Path, branch);
			StatusMessage = $"Worktree '{worktree.Name}' now on '{branch}'.";
			await LoadWorktreesAsync();
		}

		private async Task FindOrphanWorktreesAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			var orphans = GitService.FindOrphanWorktreeDirectories(repo.Path);
			if (orphans.Count == 0)
			{
				StatusMessage = "No orphaned worktree folders found.";
				return;
			}
			var answer = MessageBox.Show(
				"These folders point to worktree metadata that no longer exists:\n\n" +
				string.Join("\n", orphans) + "\n\nDelete them?",
				"Orphaned Worktree Folders", MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (answer != MessageBoxResult.Yes)
				return;
			foreach (var dir in orphans)
			{
				try { System.IO.Directory.Delete(dir, recursive: true); }
				catch (Exception e) { LastError = $"{dir}: {e.Message}"; }
			}
			await git.PruneWorktreesAsync(repo.Path);
			StatusMessage = $"Deleted {orphans.Count} orphaned folder(s).";
			await LoadWorktreesAsync();
		}

		private async Task AddWorktreeAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			var branches = await git.GetBranchesAsync(repo.Path);
			var worktrees = await git.GetWorktreesAsync(repo.Path);
			var inUse = new HashSet<string>(worktrees.Select(w => w.Branch).Where(b => !string.IsNullOrEmpty(b)), StringComparer.OrdinalIgnoreCase);

			var branchBox = new System.Windows.Controls.ComboBox
			{
				IsEditable = true,
				Margin = new Thickness(8, 2, 8, 4),
				ItemsSource = branches.Where(b => !inUse.Contains(b)).ToList()
			};
			var pathBox = new System.Windows.Controls.TextBox { Margin = new Thickness(8, 2, 8, 4) };
			var hint = new System.Windows.Controls.TextBlock
			{
				Margin = new Thickness(8, 0, 8, 4),
				FontSize = 11,
				Opacity = 0.7,
				TextWrapping = TextWrapping.Wrap
			};
			void UpdatePreview(object s, EventArgs e)
			{
				string branch = branchBox.Text?.Trim() ?? "";
				pathBox.Text = branch.Length == 0 ? "" : GitService.GetDefaultWorktreePath(repo.Path, branch);
				bool exists = branches.Contains(branch, StringComparer.OrdinalIgnoreCase);
				hint.Text = branch.Length == 0 ? "Pick an existing branch or type a new name."
					: exists ? $"Existing branch '{branch}' will be checked out into the new worktree."
					: $"New branch '{branch}' will be created (git worktree add -b).";
			}
			branchBox.AddHandler(System.Windows.Controls.TextBox.TextChangedEvent,
				new System.Windows.Controls.TextChangedEventHandler(UpdatePreview));
			branchBox.SelectionChanged += (s, e) =>
				Application.Current?.Dispatcher.BeginInvoke(new Action(() => UpdatePreview(null, EventArgs.Empty)));
			UpdatePreview(null, EventArgs.Empty);

			var buildAfter = new System.Windows.Controls.CheckBox
			{
				Content = "Run restore && build after create",
				Margin = new Thickness(8, 6, 8, 0),
				FontSize = 11
			};
			var ok = new System.Windows.Controls.Button
			{
				Content = "Create Worktree", Padding = new Thickness(12, 3, 12, 3),
				Margin = new Thickness(0, 8, 8, 8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true
			};
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Branch (existing or new):", Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7 });
			panel.Children.Add(branchBox);
			panel.Children.Add(hint);
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Worktree folder:", Margin = new Thickness(8, 4, 8, 0), Opacity = 0.7 });
			panel.Children.Add(pathBox);
			panel.Children.Add(buildAfter);
			panel.Children.Add(ok);
			var window = new Window
			{
				Title = "Add Worktree",
				Content = panel,
				Width = 460,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			window.Loaded += (s, e) => branchBox.Focus();
			if (window.ShowDialog() != true)
				return;

			string targetBranch = branchBox.Text?.Trim();
			string targetPath = pathBox.Text?.Trim();
			if (string.IsNullOrEmpty(targetBranch) || string.IsNullOrEmpty(targetPath))
				return;
			bool createBranch = !branches.Contains(targetBranch, StringComparer.OrdinalIgnoreCase);
			StatusMessage = $"Creating worktree {targetPath}…";
			await git.AddWorktreeAsync(repo.Path, targetPath, targetBranch, createBranch);
			StatusMessage = $"Worktree created: {targetPath}";
			await LoadWorktreesAsync();
			if (buildAfter.IsChecked == true)
				await BootstrapWorktreeAsync(targetPath);
		}

		/// <summary>dotnet restore + build for the solutions of a freshly created worktree (top two folder levels).</summary>
		private async Task BootstrapWorktreeAsync(string worktreePath)
		{
			var solutions = System.IO.Directory.EnumerateFiles(worktreePath, "*.sln", System.IO.SearchOption.TopDirectoryOnly)
				.Concat(System.IO.Directory.EnumerateDirectories(worktreePath)
					.SelectMany(d => System.IO.Directory.EnumerateFiles(d, "*.sln", System.IO.SearchOption.TopDirectoryOnly)))
				.ToList();
			if (solutions.Count == 0)
			{
				StatusMessage = "Worktree created — no .sln found to bootstrap (top two levels).";
				return;
			}
			foreach (string solution in solutions)
			{
				string name = System.IO.Path.GetFileName(solution);
				StatusMessage = $"dotnet restore {name}…";
				var restore = await CheckoutAndBuild.Core.Execution.ProcessRunner.RunAsync("dotnet", $"restore \"{solution}\"");
				if (!restore.Success)
				{
					LastError = $"restore failed for {name}: {restore.StdErr.Trim()}";
					continue;
				}
				StatusMessage = $"dotnet build {name}…";
				var build = await CheckoutAndBuild.Core.Execution.ProcessRunner.RunAsync("dotnet", $"build \"{solution}\"");
				if (!build.Success)
					LastError = $"build failed for {name}: {build.StdErr.Trim()}";
			}
			StatusMessage = LastError == null
				? $"Worktree bootstrapped: {solutions.Count} solution(s) restored and built."
				: "Worktree bootstrap finished with errors — see the error line.";
		}

		private void RemoveWorktreeWithConfirm(WorktreeViewModel worktree)
		{
			if (worktree == null || worktree.Worktree.IsMain)
				return;
			var force = new System.Windows.Controls.CheckBox
			{
				Content = "Force (also with uncommitted changes)",
				Margin = new Thickness(8, 4, 8, 0),
				FontSize = 11
			};
			var deleteBranch = new System.Windows.Controls.CheckBox
			{
				Content = $"Also delete branch '{worktree.Worktree.Branch}'",
				Margin = new Thickness(8, 4, 8, 0),
				FontSize = 11,
				IsEnabled = !worktree.Worktree.IsDetached && !string.IsNullOrEmpty(worktree.Worktree.Branch)
			};
			var ok = new System.Windows.Controls.Button
			{
				Content = "Remove", Padding = new Thickness(12, 3, 12, 3),
				Margin = new Thickness(0, 8, 8, 8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true
			};
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = $"Remove worktree '{worktree.Name}'?\n{worktree.Path}\n\nThe folder is deleted.",
				Margin = new Thickness(8, 8, 8, 0),
				TextWrapping = TextWrapping.Wrap
			});
			panel.Children.Add(force);
			panel.Children.Add(deleteBranch);
			panel.Children.Add(ok);
			var window = new Window
			{
				Title = "Remove Worktree",
				Content = panel,
				Width = 420,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			if (window.ShowDialog() != true)
				return;
			RunSafe(async () =>
			{
				await git.RemoveWorktreeAsync(SelectedRepository.Path, worktree.Path, force.IsChecked == true);
				if (deleteBranch.IsChecked == true)
					await git.DeleteBranchAsync(SelectedRepository.Path, worktree.Worktree.Branch, force: force.IsChecked == true);
				StatusMessage = $"Worktree removed: {worktree.Name}";
				await LoadWorktreesAsync();
			});
		}

		private async Task PruneWorktreesAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			await git.PruneWorktreesAsync(repo.Path);
			StatusMessage = "Pruned stale worktree entries.";
			await LoadWorktreesAsync();
		}

		private void OpenWorktreeSolution(WorktreeViewModel worktree)
		{
			if (worktree == null || !worktree.Worktree.Exists)
				return;
			string solution = System.IO.Directory.EnumerateFiles(worktree.Path, "*.sln", System.IO.SearchOption.TopDirectoryOnly)
				.Concat(System.IO.Directory.EnumerateDirectories(worktree.Path)
					.SelectMany(d => System.IO.Directory.EnumerateFiles(d, "*.sln", System.IO.SearchOption.TopDirectoryOnly)))
				.FirstOrDefault();
			if (solution == null)
			{
				StatusMessage = "No .sln found in the worktree (top two levels).";
				return;
			}
			try
			{
				string devenv = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
				if (devenv != null)
					System.Diagnostics.Process.Start(devenv, $"\"{solution}\"");
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		private void AddWorktreeAsWorkingFolder(WorktreeViewModel worktree)
		{
			if (worktree == null)
				return;
			try
			{
				MainViewModel.Shared.AddFolderByPath(worktree.Path);
				StatusMessage = $"Added as working folder: {worktree.Path}";
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		#endregion

		public string MultiRepoBranch
		{
			get { return multiRepoBranch; }
			set { SetProperty(ref multiRepoBranch, value); }
		}

		public bool CreateBranchIfMissing
		{
			get { return createBranchIfMissing; }
			set { SetProperty(ref createBranchIfMissing, value); }
		}

		private async Task CheckoutAllAsync()
		{
			string branch = MultiRepoBranch.Trim();
			int done = 0, created = 0, skipped = 0;
			foreach (var row in SyncRows.ToList())
			{
				try
				{
					StatusMessage = $"Checkout {branch}: {row.Name}…";
					if (await git.BranchExistsAsync(row.Path, branch))
					{
						await git.CheckoutBranchAsync(row.Path, branch);
						done++;
					}
					else if (CreateBranchIfMissing)
					{
						await git.CreateBranchAsync(row.Path, branch);
						created++;
					}
					else
					{
						skipped++;
					}
					row.Status = null;
				}
				catch (Exception e)
				{
					row.Status = e.Message;
					skipped++;
				}
				await UpdateSyncRowAsync(row);
			}
			StatusMessage = $"Checkout '{branch}': {done} switched, {created} created, {skipped} skipped.";
			await RefreshRepositoryAsync();
		}

		private async Task CleanupMergedBranchesAsync()
		{
			StatusMessage = "Scanning merged branches…";
			var candidates = new List<(RepoSyncViewModel Repo, string Branch)>();
			foreach (var row in SyncRows.ToList())
			{
				try
				{
					string target = await git.GetDefaultBranchAsync(row.Path);
					if (target == null)
						continue;
					foreach (string branch in await git.GetMergedBranchesAsync(row.Path, target))
						candidates.Add((row, branch));
				}
				catch (Exception e)
				{
					row.Status = e.Message;
				}
			}
			if (candidates.Count == 0)
			{
				StatusMessage = "No merged branches to clean up.";
				return;
			}

			var list = new System.Windows.Controls.ListBox
			{
				Margin = new Thickness(8),
				MaxHeight = 320,
				SelectionMode = System.Windows.Controls.SelectionMode.Multiple
			};
			foreach (var candidate in candidates)
				list.Items.Add($"{candidate.Repo.Name}: {candidate.Branch}");
			list.SelectAll();
			var ok = new System.Windows.Controls.Button
			{
				Content = "Delete selected", Padding = new Thickness(12, 3, 12, 3),
				Margin = new Thickness(0, 0, 8, 8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true
			};
			var panel = new System.Windows.Controls.DockPanel();
			System.Windows.Controls.DockPanel.SetDock(ok, System.Windows.Controls.Dock.Bottom);
			panel.Children.Add(ok);
			panel.Children.Add(list);
			var window = new Window
			{
				Title = "Cleanup Merged Branches",
				Content = panel,
				Width = 420,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			if (window.ShowDialog() != true)
			{
				StatusMessage = null;
				return;
			}

			int deleted = 0;
			for (int i = 0; i < candidates.Count; i++)
			{
				if (!list.SelectedItems.Contains(list.Items[i]))
					continue;
				try
				{
					await git.DeleteBranchAsync(candidates[i].Repo.Path, candidates[i].Branch);
					deleted++;
				}
				catch (Exception e)
				{
					candidates[i].Repo.Status = e.Message;
				}
			}
			StatusMessage = $"Deleted {deleted} merged branch(es).";
			await LoadBranchesAsync();
		}

		private async Task CreatePullRequestAsync(RepoSyncViewModel row)
		{
			if (row == null)
				return;
			string remote = await git.GetRemoteUrlAsync(row.Path);
			if (remote == null)
			{
				row.Status = "No origin remote configured.";
				return;
			}
			string branch = await git.GetCurrentBranchAsync(row.Path);
			string url = BuildPullRequestUrl(remote, branch);
			if (url == null)
			{
				row.Status = "Unknown host — cannot build a PR URL for: " + remote;
				return;
			}
			System.Diagnostics.Process.Start(url);
		}

		internal static string BuildPullRequestUrl(string remoteUrl, string branch)
		{
			string url = remoteUrl.Trim();
			if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
				url = url.Substring(0, url.Length - 4);
			if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
				url = "https://" + url.Substring(4).Replace(":", "/");

			string escapedBranch = Uri.EscapeDataString(branch);
			if (url.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
				return $"{url}/compare/{escapedBranch}?expand=1";
			if (url.IndexOf("dev.azure.com", StringComparison.OrdinalIgnoreCase) >= 0
				|| url.IndexOf("visualstudio.com", StringComparison.OrdinalIgnoreCase) >= 0
				|| url.IndexOf("/_git/", StringComparison.OrdinalIgnoreCase) >= 0)
				return $"{url}/pullrequestcreate?sourceRef={escapedBranch}";
			return null;
		}
		public ICommand StashPushCommand { get; }
		public ICommand StashApplyCommand { get; }
		public ICommand StashPopCommand { get; }
		public ICommand StashDropCommand { get; }
		public ICommand CheckoutBranchCommand { get; }
		public ICommand CreateBranchCommand { get; }
		public ICommand DeleteBranchCommand { get; }
		public ICommand RefreshBranchesCommand { get; }
		public ICommand FetchRepoCommand { get; }
		public ICommand PullRepoCommand { get; }
		public ICommand PushRepoCommand { get; }
		public ICommand FetchAllCommand { get; }
		public ICommand PullAllCommand { get; }
		public ICommand PushAllCommand { get; }
		public ICommand RefreshSyncCommand { get; }
		public ICommand RefreshHistoryCommand { get; }
		public ICommand CopyShaCommand { get; }
		public ICommand RefreshFeedCommand { get; }

		private bool HasRepository => SelectedRepository != null;

		public GitRepositoryViewModel SelectedRepository
		{
			get { return selectedRepository; }
			set
			{
				if (SetProperty(ref selectedRepository, value) && value != null)
					RunSafe(RefreshRepositoryAsync);
			}
		}

		public ChangeViewModel SelectedChange
		{
			get { return selectedChange; }
			set
			{
				if (SetProperty(ref selectedChange, value) && value != null)
					RunSafe(LoadChangeDiffAsync);
			}
		}

		public StashViewModel SelectedStash
		{
			get { return selectedStash; }
			set
			{
				if (SetProperty(ref selectedStash, value))
				{
					CommandManager.InvalidateRequerySuggested();
					if (value != null)
						RunSafe(LoadStashDiffAsync);
					else
						StashDiffText = null;
				}
			}
		}

		public GitCommit SelectedCommit
		{
			get { return selectedCommit; }
			set
			{
				if (SetProperty(ref selectedCommit, value))
				{
					CommandManager.InvalidateRequerySuggested();
					if (value != null)
						RunSafe(LoadCommitFilesAsync);
					else
					{
						CommitFiles.Clear();
						CommitFileDiffText = null;
					}
				}
			}
		}

		public GitCommitFile SelectedCommitFile
		{
			get { return selectedCommitFile; }
			set
			{
				if (SetProperty(ref selectedCommitFile, value))
				{
					if (value != null)
						RunSafe(LoadCommitFileDiffAsync);
					else
						CommitFileDiffText = null;
				}
			}
		}

		public BranchViewModel SelectedBranch
		{
			get { return selectedBranch; }
			set
			{
				if (SetProperty(ref selectedBranch, value))
					CommandManager.InvalidateRequerySuggested();
			}
		}

		public int SelectedTabIndex
		{
			get { return selectedTabIndex; }
			set
			{
				if (SetProperty(ref selectedTabIndex, value))
				{
					if (value == syncTabIndex && SyncRows.Count == 0)
						RunSafe(RefreshSyncAsync);
					else if (value == feedTabIndex && FeedCommits.Count == 0)
						RunSafe(RefreshFeedAsync);
					else if (value == worktreesTabIndex && Worktrees.Count == 0)
						RunSafe(LoadWorktreesAsync);
				}
			}
		}

		public string DiffText
		{
			get { return diffText; }
			private set { SetProperty(ref diffText, value); }
		}

		public string CommitFileDiffText
		{
			get { return commitFileDiffText; }
			private set { SetProperty(ref commitFileDiffText, value); }
		}

		public string StashDiffText
		{
			get { return stashDiffText; }
			private set { SetProperty(ref stashDiffText, value); }
		}

		public string NewStashMessage
		{
			get { return newStashMessage; }
			set { SetProperty(ref newStashMessage, value); }
		}

		public string NewBranchName
		{
			get { return newBranchName; }
			set { SetProperty(ref newBranchName, value); }
		}

		public string HistoryGrep
		{
			get { return historyGrep; }
			set { SetProperty(ref historyGrep, value); }
		}

		public string HistoryAuthor
		{
			get { return historyAuthor; }
			set { SetProperty(ref historyAuthor, value); }
		}

		public bool OnlyMine
		{
			get { return onlyMine; }
			set { SetProperty(ref onlyMine, value); }
		}

		public int HistoryPeriodIndex
		{
			get { return historyPeriodIndex; }
			set { SetProperty(ref historyPeriodIndex, value); }
		}

		private int? SinceDays => periodDays[historyPeriodIndex >= 0 && historyPeriodIndex < periodDays.Length ? historyPeriodIndex : 0];

		public bool IsBusy
		{
			get { return isBusy; }
			private set
			{
				if (SetProperty(ref isBusy, value))
					CommandManager.InvalidateRequerySuggested();
			}
		}

		public string LastError
		{
			get { return lastError; }
			private set { SetProperty(ref lastError, value); }
		}

		public string StatusMessage
		{
			get { return statusMessage; }
			private set { SetProperty(ref statusMessage, value); }
		}

		/// <summary>Discovers the repositories once (tool window Loaded). Safe to call multiple times.</summary>
		public Task LoadAsync()
		{
			return loadTask ?? (loadTask = GuardedAsync(RefreshAllAsync));
		}

		/// <summary>
		/// Live working folders: the main window's current list when it exists (folders added there are
		/// visible immediately), otherwise a fresh settings read — the ctor-time settings copy goes stale.
		/// </summary>
		private List<string> CurrentWorkingFolders()
		{
			var shared = MainViewModel.Shared;
			if (shared != null && shared.WorkingFolders.Count > 0)
				return shared.WorkingFolders.Select(f => f.Path).ToList();
			return JsonSettingsService.CreateDefault().Get<List<string>>(workingFoldersKey, globalContext) ?? new List<string>();
		}

		/// <summary>Selects the repository and switches to the Changes tab (jump from the main window).</summary>
		public async Task ShowRepositoryAsync(string repositoryPath)
		{
			await LoadAsync();
			var repo = Repositories.FirstOrDefault(r => string.Equals(r.Path, repositoryPath, StringComparison.OrdinalIgnoreCase));
			if (repo == null)
			{
				repo = new GitRepositoryViewModel(repositoryPath);
				Repositories.Add(repo);
			}
			SelectedTabIndex = 0;
			SelectedRepository = repo;
		}

		/// <summary>Selects the repository containing <paramref name="repositoryPath"/> and switches to the History tab.</summary>
		public async Task ShowHistoryAsync(string repositoryPath)
		{
			await LoadAsync();
			var repo = Repositories.FirstOrDefault(r => string.Equals(r.Path, repositoryPath, StringComparison.OrdinalIgnoreCase));
			if (repo == null)
			{
				repo = new GitRepositoryViewModel(repositoryPath);
				Repositories.Add(repo);
			}
			SelectedTabIndex = 2;
			SelectedRepository = repo;
		}

		private async void RunSafe(Func<Task> action) => await GuardedAsync(action);

		private async Task GuardedAsync(Func<Task> action)
		{
			try
			{
				LastError = null;
				StatusMessage = null;
				IsBusy = true;
				await action();
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async Task RefreshAllAsync()
		{
			var folders = CurrentWorkingFolders();
			var previous = SelectedRepository?.Path;
			var roots = await Task.Run(() => FindRepositoryRoots(folders));

			Repositories.Clear();
			foreach (var entry in roots)
			{
				var repo = new GitRepositoryViewModel(entry.Key)
				{
					Folder = System.IO.Path.GetFileName(entry.Value.TrimEnd(System.IO.Path.DirectorySeparatorChar))
				};
				try
				{
					repo.Branch = await git.GetCurrentBranchAsync(entry.Key);
				}
				catch (Exception e)
				{
					System.Diagnostics.Trace.WriteLine("CheckoutAndBuild branch load failed: " + e.Message);
				}
				Repositories.Add(repo);
			}

			SelectedRepository = Repositories.FirstOrDefault(r => string.Equals(r.Path, previous, StringComparison.OrdinalIgnoreCase))
								 ?? Repositories.FirstOrDefault();
			if (SelectedRepository == null)
				StatusMessage = "No git repositories found beneath the configured working folders.";

			SyncRows.Clear();
			FeedCommits.Clear();
			if (SelectedTabIndex == syncTabIndex)
				await RefreshSyncAsync();
			else if (SelectedTabIndex == feedTabIndex)
				await RefreshFeedAsync();
		}

		public ObservableCollection<string> HeaderBranches { get; } = new ObservableCollection<string>();

		private bool suppressHeaderCheckout;
		private string selectedHeaderBranch;

		/// <summary>Branch dropdown in the window header — picking a different branch checks it out (auto-stash honored).</summary>
		public string SelectedHeaderBranch
		{
			get { return selectedHeaderBranch; }
			set
			{
				if (SetProperty(ref selectedHeaderBranch, value) && !suppressHeaderCheckout
					&& value != null && SelectedRepository != null && value != SelectedRepository.Branch)
					RunSafe(() => CheckoutHeaderBranchAsync(value));
			}
		}

		private async Task CheckoutHeaderBranchAsync(string branch)
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			bool autoStash = JsonSettingsService.CreateDefault().Get("AutoStash", globalContext, true);
			bool stashed = await git.AutoStashAsync(repo.Path, autoStash);
			try
			{
				await git.CheckoutBranchAsync(repo.Path, branch);
			}
			finally
			{
				if (stashed && !await git.TryAutoStashPopAsync(repo.Path))
					LastError = $"Auto-stash restore conflicted in {repo.Name} — your changes remain in stash@{{0}}.";
			}
			StatusMessage = $"{repo.Name} → {branch}";
			await RefreshRepositoryAsync();
		}

		private async Task RefreshRepositoryAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;

			repo.Branch = await git.GetCurrentBranchAsync(repo.Path);
			suppressHeaderCheckout = true;
			HeaderBranches.Clear();
			foreach (string name in await git.GetBranchesAsync(repo.Path))
				HeaderBranches.Add(name);
			SelectedHeaderBranch = repo.Branch;
			suppressHeaderCheckout = false;

			var status = await git.GetStatusAsync(repo.Path);
			Changes.Clear();
			foreach (var change in status.Select(c => new ChangeViewModel(c, repo.Path)).OrderBy(c => c.GroupOrder).ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase))
				Changes.Add(change);
			DiffText = null;

			var stashes = await git.GetStashesAsync(repo.Path);
			Stashes.Clear();
			for (int i = 0; i < stashes.Count; i++)
				Stashes.Add(new StashViewModel(stashes[i], i));
			StashDiffText = null;

			await LoadHistoryAsync();
			await LoadBranchesAsync();
		}

		private async Task<string> GetEffectiveAuthorAsync(string repoDir)
		{
			if (OnlyMine)
				return await git.GetConfiguredUserAsync(repoDir);
			return string.IsNullOrWhiteSpace(HistoryAuthor) ? null : HistoryAuthor.Trim();
		}

		private async Task LoadHistoryAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			Commits.Clear();
			CommitFiles.Clear();
			CommitFileDiffText = null;
			try
			{
				var author = await GetEffectiveAuthorAsync(repo.Path);
				var grep = string.IsNullOrWhiteSpace(HistoryGrep) ? null : HistoryGrep.Trim();
				foreach (var commit in await git.GetHistoryAsync(repo.Path, 100, author, SinceDays, grep, HistoryPathFilter))
					Commits.Add(commit);
			}
			catch (InvalidOperationException)
			{
			}
		}

		private async Task LoadCommitFilesAsync()
		{
			var commit = SelectedCommit;
			var repo = SelectedRepository;
			if (commit == null || repo == null)
				return;
			var files = await git.GetCommitFilesAsync(repo.Path, commit.Sha);
			CommitFiles.Clear();
			foreach (var file in files)
				CommitFiles.Add(file);
			CommitFileDiffText = null;
		}

		private async Task LoadCommitFileDiffAsync()
		{
			var file = SelectedCommitFile;
			var commit = SelectedCommit;
			var repo = SelectedRepository;
			if (file == null || commit == null || repo == null)
				return;
			CommitFileDiffText = await git.GetFileDiffAsync(repo.Path, commit.Sha, file.FilePath);
		}

		private async Task LoadBranchesAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			Branches.Clear();
			foreach (var name in await git.GetBranchesAsync(repo.Path))
				Branches.Add(new BranchViewModel(name, name == repo.Branch));
			foreach (var branch in Branches.ToList())
			{
				var sync = await git.GetAheadBehindAsync(repo.Path, branch.Name);
				branch.SyncBadge = FormatBadge(sync);
			}
		}

		private static string FormatBadge(BranchSyncStatus sync)
		{
			return sync.HasUpstream ? $"↑{sync.Ahead} ↓{sync.Behind}" : null;
		}

		private async Task CheckoutSelectedBranchAsync()
		{
			var repo = SelectedRepository;
			var branch = SelectedBranch;
			if (repo == null || branch == null)
				return;
			await git.CheckoutBranchAsync(repo.Path, branch.Name);
			StatusMessage = $"Checked out {branch.Name}.";
			await RefreshRepositoryAsync();
		}

		private async Task CreateBranchAsync()
		{
			var repo = SelectedRepository;
			var name = NewBranchName?.Trim();
			if (repo == null || string.IsNullOrEmpty(name))
				return;
			await git.CreateBranchAsync(repo.Path, name);
			NewBranchName = null;
			StatusMessage = $"Created and checked out {name}.";
			await RefreshRepositoryAsync();
		}

		private async Task DeleteBranchAsync()
		{
			var repo = SelectedRepository;
			var branch = SelectedBranch;
			if (repo == null || branch == null || branch.IsCurrent)
				return;
			if (MessageBox.Show($"Delete branch \"{branch.Name}\"?", "Delete branch",
					MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
				return;
			try
			{
				await git.DeleteBranchAsync(repo.Path, branch.Name);
			}
			catch (InvalidOperationException e)
			{
				if (MessageBox.Show($"{e.Message}\n\nForce delete (git branch -D)?", "Delete branch",
						MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
					return;
				await git.DeleteBranchAsync(repo.Path, branch.Name, force: true);
			}
			StatusMessage = $"Deleted {branch.Name}.";
			await LoadBranchesAsync();
		}

		private async Task RefreshSyncAsync()
		{
			SyncRows.Clear();
			foreach (var repo in Repositories)
				SyncRows.Add(new RepoSyncViewModel(repo, (row, branch) => RunSafe(() => CheckoutSyncRowAsync(row, branch))));
			foreach (var row in SyncRows.ToList())
				await UpdateSyncRowAsync(row);
		}

		private async Task UpdateSyncRowAsync(RepoSyncViewModel row)
		{
			try
			{
				row.Branch = await git.GetCurrentBranchAsync(row.Path);
				var sync = await git.GetAheadBehindAsync(row.Path, row.Branch);
				row.HasUpstream = sync.HasUpstream;
				row.SyncBadge = sync.HasUpstream ? FormatBadge(sync) : "no upstream";
				row.SetBranches(await git.GetBranchesAsync(row.Path), row.Branch);
				row.Status = null;
			}
			catch (Exception e)
			{
				row.Status = e.Message;
			}
		}

		/// <summary>Checkout picked in a Sync row's branch dropdown (auto-stash honored).</summary>
		private async Task CheckoutSyncRowAsync(RepoSyncViewModel row, string branch)
		{
			StatusMessage = $"checkout {branch}: {row.Name}…";
			try
			{
				bool autoStash = JsonSettingsService.CreateDefault().Get("AutoStash", globalContext, true);
				bool stashed = await git.AutoStashAsync(row.Path, autoStash);
				try
				{
					await git.CheckoutBranchAsync(row.Path, branch);
				}
				finally
				{
					if (stashed && !await git.TryAutoStashPopAsync(row.Path))
						row.Status = "Auto-stash restore conflicted — your changes remain in stash@{0}.";
				}
				StatusMessage = $"{row.Name} → {branch}";
				row.Repository.Branch = branch;
			}
			catch (Exception e)
			{
				row.Status = e.Message;
			}
			await UpdateSyncRowAsync(row);
		}

		private async Task SyncRowActionAsync(RepoSyncViewModel row, string action)
		{
			if (row == null)
				return;
			StatusMessage = $"{action}: {row.Name}…";
			try
			{
				await RunSyncActionAsync(row, action);
				await UpdateSyncRowAsync(row);
				StatusMessage = $"{action}: {row.Name} done.";
			}
			catch (Exception e)
			{
				row.Status = e.Message;
				StatusMessage = $"{action}: {row.Name} failed.";
			}
			if (row.Repository == SelectedRepository && action != "fetch")
				await RefreshRepositoryAsync();
		}

		private Task RunSyncActionAsync(RepoSyncViewModel row, string action)
		{
			switch (action)
			{
				case "fetch": return git.FetchAsync(row.Path);
				case "pull": return git.PullAsync(row.Path);
				case "force-push": return git.ForcePushAsync(row.Path);
				default: return git.PushAsync(row.Path, setUpstream: !row.HasUpstream);
			}
		}

		private void ForcePushWithConfirm(RepoSyncViewModel row)
		{
			if (row == null)
				return;
			if (MessageBox.Show(
					$"Force push '{row.Branch}' of {row.Name}?\n\nUses --force-with-lease: the push is refused when the remote moved since your last fetch, but it still overwrites the remote branch.",
					"CheckoutAndBuild", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
				return;
			RunSafe(() => SyncRowActionAsync(row, "force-push"));
		}

		private async Task SyncAllAsync(string action)
		{
			var rows = SyncRows.ToList();
			var errors = new List<string>();
			for (int i = 0; i < rows.Count; i++)
			{
				var row = rows[i];
				StatusMessage = $"{action} {row.Name} ({i + 1}/{rows.Count})…";
				try
				{
					await RunSyncActionAsync(row, action);
					await UpdateSyncRowAsync(row);
				}
				catch (Exception e)
				{
					row.Status = e.Message;
					errors.Add($"{row.Name}: {e.Message}");
				}
			}
			StatusMessage = $"{action} completed for {rows.Count} repositories" + (errors.Count > 0 ? $" ({errors.Count} failed)." : ".");
			if (errors.Count > 0)
				LastError = string.Join("\n", errors);
			if (action != "fetch")
				await RefreshRepositoryAsync();
		}

		private async Task RefreshFeedAsync()
		{
			var grep = string.IsNullOrWhiteSpace(HistoryGrep) ? null : HistoryGrep.Trim();
			var list = new List<FeedCommitViewModel>();
			foreach (var repo in Repositories.ToList())
			{
				try
				{
					var author = await GetEffectiveAuthorAsync(repo.Path);
					foreach (var commit in await git.GetHistoryAsync(repo.Path, 30, author, SinceDays, grep))
						list.Add(new FeedCommitViewModel(repo, commit));
				}
				catch (InvalidOperationException)
				{
				}
			}
			FeedCommits.Clear();
			foreach (var entry in list.OrderByDescending(f => f.SortDate))
				FeedCommits.Add(entry);
			if (FeedCommits.Count == 0)
				StatusMessage = "No commits match the current filters.";
		}

		/// <summary>Feed double-click: switches to the History tab of the commit's repository and selects it.</summary>
		public Task ShowCommitInHistoryAsync(FeedCommitViewModel feedCommit)
		{
			if (feedCommit == null)
				return Task.CompletedTask;
			return GuardedAsync(async () =>
			{
				SelectedTabIndex = historyTabIndex;
				if (!ReferenceEquals(SelectedRepository, feedCommit.Repository))
				{
					selectedRepository = feedCommit.Repository;
					RaisePropertyChanged(nameof(SelectedRepository));
					await RefreshRepositoryAsync();
				}
				SelectedCommit = Commits.FirstOrDefault(c => c.Sha == feedCommit.Commit.Sha);
			});
		}

		private async Task LoadChangeDiffAsync()
		{
			var change = SelectedChange;
			var repo = SelectedRepository;
			if (change == null || repo == null)
				return;

			if (change.Change.ChangeType == GitChangeType.Untracked)
				DiffText = "(untracked file — no diff available)";
			else
				DiffText = await git.GetDiffAsync(repo.Path, change.FilePath, change.Change.IsStaged);
		}

		private async Task LoadStashDiffAsync()
		{
			var stash = SelectedStash;
			var repo = SelectedRepository;
			if (stash == null || repo == null)
				return;
			StashDiffText = await git.GetStashDiffAsync(repo.Path, stash.Index);
		}

		private async Task StashPushAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			await git.StashPushAsync(repo.Path, string.IsNullOrWhiteSpace(NewStashMessage) ? null : NewStashMessage.Trim());
			NewStashMessage = null;
			await RefreshRepositoryAsync();
		}

		private async Task StashActionAsync(string action)
		{
			var repo = SelectedRepository;
			var stash = SelectedStash;
			if (repo == null || stash == null)
				return;
			if (action == "apply")
				await git.StashApplyAsync(repo.Path, stash.Index);
			else
				await git.StashPopAsync(repo.Path, stash.Index);
			await RefreshRepositoryAsync();
		}

		private async Task StashDropAsync()
		{
			var repo = SelectedRepository;
			var stash = SelectedStash;
			if (repo == null || stash == null)
				return;
			if (MessageBox.Show($"Drop {stash.Id} \"{stash.Name}\"?\nThis cannot be undone.", "Drop stash",
					MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
				return;
			await git.StashDropAsync(repo.Path, stash.Index);
			await RefreshRepositoryAsync();
		}

		#region change file actions (VS-like git changes context menu)

		private async Task CompareChangeAsync(ChangeViewModel change)
		{
			var repo = SelectedRepository;
			if (change == null || repo == null)
				return;
			string headFile = await git.GetHeadVersionToTempFileAsync(repo.Path, change.FilePath);
			if (headFile == null)
			{
				StatusMessage = "No HEAD version of this file (new file?).";
				return;
			}

			await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			var differenceService = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider
				.GetService(typeof(Microsoft.VisualStudio.Shell.Interop.SVsDifferenceService))
				as Microsoft.VisualStudio.Shell.Interop.IVsDifferenceService;
			if (differenceService == null)
			{
				StatusMessage = "VS difference service not available.";
				return;
			}
			string fileName = System.IO.Path.GetFileName(change.FilePath);
			differenceService.OpenComparisonWindow2(
				headFile, change.FullPath,
				$"{fileName} (HEAD) ↔ {fileName}",
				change.FilePath,
				$"{fileName} (HEAD)", fileName,
				null, null,
				(uint)Microsoft.VisualStudio.Shell.Interop.__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary);
		}

		private void OpenChange(ChangeViewModel change)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			if (change != null && System.IO.File.Exists(change.FullPath))
				Microsoft.VisualStudio.Shell.VsShellUtilities.OpenDocument(
					Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, change.FullPath);
		}

		private async Task ShowFileHistoryAsync(ChangeViewModel change)
		{
			if (change == null)
				return;
			HistoryPathFilter = change.FilePath;
			SelectedTabIndex = historyTabIndex;
			await LoadHistoryAsync();
		}

		private async Task StageChangeAsync(ChangeViewModel change, bool stage)
		{
			var repo = SelectedRepository;
			if (change == null || repo == null)
				return;
			if (stage)
				await git.StageAsync(repo.Path, change.FilePath);
			else
				await git.UnstageAsync(repo.Path, change.FilePath);
			await RefreshRepositoryAsync();
		}

		private void DiscardChangeWithConfirm(ChangeViewModel change)
		{
			if (change == null)
				return;
			bool untracked = change.Change.ChangeType == Core.Git.GitChangeType.Untracked;
			string question = untracked
				? $"Delete untracked file '{change.FilePath}'?"
				: $"Undo all changes in '{change.FilePath}'? This restores the HEAD version.";
			if (MessageBox.Show(question + "\n\nThis cannot be undone.", "CheckoutAndBuild",
					MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
				return;
			RunSafe(async () =>
			{
				await git.DiscardAsync(SelectedRepository.Path, change.FilePath, untracked);
				await RefreshRepositoryAsync();
			});
		}

		private bool CanCommit() =>
			HasRepository && !IsBusy && Changes.Count > 0 && !string.IsNullOrWhiteSpace(CommitMessage);

		private async Task CommitAllAsync(bool push)
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			StatusMessage = "Committing…";
			await git.CommitAllAsync(repo.Path, CommitMessage.Trim());
			CommitMessage = null;
			if (push)
			{
				StatusMessage = "Pushing…";
				var status = await git.GetAheadBehindAsync(repo.Path);
				await git.PushAsync(repo.Path, setUpstream: !status.HasUpstream);
			}
			StatusMessage = push ? "Committed and pushed." : "Committed.";
			await RefreshRepositoryAsync();
		}

		#endregion

		private async Task ApplyPatchAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Patch File|*.patch;*.diff|All files|*.*" };
			if (dialog.ShowDialog() != true)
				return;
			await git.ApplyPatchAsync(repo.Path, dialog.FileName);
			StatusMessage = "Applied: " + dialog.FileName;
			await RefreshRepositoryAsync();
		}

		private async Task SuggestBranchNameAsync()
		{
			var idBox = new System.Windows.Controls.TextBox { Margin = new Thickness(8, 2, 8, 4) };
			var prefixBox = new System.Windows.Controls.ComboBox
			{
				ItemsSource = new[] { "wip", "feature", "bugfix", "hotfix" },
				SelectedIndex = 0,
				Margin = new Thickness(8, 2, 8, 4)
			};
			var ok = new System.Windows.Controls.Button { Content = "Suggest", Width = 80, Margin = new Thickness(0, 8, 8, 8), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Work item id:", Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7 });
			panel.Children.Add(idBox);
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Branch prefix:", Margin = new Thickness(8, 4, 8, 0), Opacity = 0.7 });
			panel.Children.Add(prefixBox);
			panel.Children.Add(ok);
			var window = new Window
			{
				Title = "Branch Suggestion",
				Content = panel,
				Width = 320,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			window.Loaded += (s, e) => idBox.Focus();
			if (window.ShowDialog() != true || !int.TryParse(idBox.Text?.Trim(), out int workItemId))
				return;

			string prefix = prefixBox.SelectedItem as string ?? "wip";
			string title = await TryGetWorkItemTitleAsync(workItemId);
			NewBranchName = string.IsNullOrEmpty(title)
				? $"{prefix}/{workItemId}"
				: $"{prefix}/{workItemId}-{Slugify(title)}";
			StatusMessage = string.IsNullOrEmpty(title)
				? "No work item title found (check the Work Items connection) — suggested the id only."
				: "Suggested: " + NewBranchName;
		}

		/// <summary>Prefills the commit message as "AB#id: title" — the id defaults to the number in the current branch name.</summary>
		private async Task CommitMessageFromWorkItemAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			string branch = await git.GetCurrentBranchAsync(repo.Path);
			string guessed = System.Text.RegularExpressions.Regex.Match(branch ?? "", @"\d{2,}").Value;

			var idBox = new System.Windows.Controls.TextBox { Margin = new Thickness(8, 2, 8, 4), Text = guessed };
			var ok = new System.Windows.Controls.Button
			{
				Content = "Prefill", Padding = new Thickness(12, 3, 12, 3),
				Margin = new Thickness(0, 8, 8, 8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true
			};
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Work item id (uses the Work Items connection):",
				Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7
			});
			panel.Children.Add(idBox);
			panel.Children.Add(ok);
			var window = new Window
			{
				Title = "Commit Message from Work Item",
				Content = panel,
				Width = 320,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			window.Loaded += (s, e) => { idBox.Focus(); idBox.SelectAll(); };
			if (window.ShowDialog() != true || !int.TryParse(idBox.Text?.Trim(), out int workItemId))
				return;

			string title = await TryGetWorkItemTitleAsync(workItemId);
			CommitMessage = string.IsNullOrEmpty(title) ? $"AB#{workItemId}: " : $"AB#{workItemId}: {title}";
			if (string.IsNullOrEmpty(title))
				StatusMessage = "No work item title found (check the Work Items connection) — prefilled the id only.";
		}

		private async Task<string> TryGetWorkItemTitleAsync(int workItemId)
		{
			try
			{
				string orgUrl = settings.Get("WorkItems.OrganizationUrl", globalContext, "");
				string project = settings.Get("WorkItems.Project", globalContext, "");
				string pat = Common.PatProtector.Unprotect(settings.Get("WorkItems.PatProtected", globalContext, ""));
				if (string.IsNullOrEmpty(orgUrl) || string.IsNullOrEmpty(pat))
					return null;
				using (var client = new CheckoutAndBuild.Core.WorkItems.WorkItemClient(orgUrl, project, pat))
				{
					var items = await client.GetWorkItemsAsync(new[] { workItemId });
					return items.Count > 0 ? items[0].Title : null;
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static string Slugify(string text)
		{
			var builder = new System.Text.StringBuilder();
			foreach (char c in text.ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(c))
					builder.Append(c);
				else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
					builder.Append('-');
			}
			return builder.ToString().Trim('-');
		}

		private async Task ExportPatchAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Patch File|*.patch",
				FileName = repo.Name + "-changes.patch",
				DefaultExt = ".patch"
			};
			if (dialog.ShowDialog() != true)
				return;
			await git.ExportChangesAsPatchAsync(repo.Path, dialog.FileName);
			StatusMessage = "Exported: " + dialog.FileName;
		}

		private async Task ExportZipAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Zip Archive|*.zip",
				FileName = repo.Name + "-changes.zip",
				DefaultExt = ".zip"
			};
			if (dialog.ShowDialog() != true)
				return;
			await git.ExportChangesAsZipAsync(repo.Path, dialog.FileName);
			StatusMessage = "Exported: " + dialog.FileName;
		}

		private static List<KeyValuePair<string, string>> FindRepositoryRoots(IEnumerable<string> workingFolders)
		{
			var roots = new List<KeyValuePair<string, string>>();
			foreach (string folder in workingFolders.Where(Directory.Exists))
			{
				string currentFolder = folder;
				Scan(currentFolder, 0);

				void Scan(string directory, int depth)
				{
					try
					{
						string dotGit = Path.Combine(directory, ".git");
						if (Directory.Exists(dotGit) || File.Exists(dotGit))
						{
							if (!roots.Any(r => string.Equals(r.Key, directory, StringComparison.OrdinalIgnoreCase)))
								roots.Add(new KeyValuePair<string, string>(directory, currentFolder));
							return;
						}
						if (depth >= maxScanDepth)
							return;
						foreach (string sub in Directory.EnumerateDirectories(directory))
						{
							if (skippedDirectories.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
								continue;
							Scan(sub, depth + 1);
						}
					}
					catch (UnauthorizedAccessException)
					{
					}
					catch (IOException)
					{
					}
				}
			}
			return roots;
		}
	}
}
