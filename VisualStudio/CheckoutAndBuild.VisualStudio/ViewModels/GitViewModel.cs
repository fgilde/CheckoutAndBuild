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

		public string Branch
		{
			get { return branch; }
			set { SetProperty(ref branch, value); }
		}
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

		/// <summary>"↑n ↓m" against the upstream, null without one.</summary>
		public string SyncBadge
		{
			get { return syncBadge; }
			set { SetProperty(ref syncBadge, value); }
		}
	}

	/// <summary>One row of the Sync tab (one repository).</summary>
	public class RepoSyncViewModel : NotificationObject
	{
		private string branch;
		private string syncBadge;
		private string status;

		public RepoSyncViewModel(GitRepositoryViewModel repository)
		{
			Repository = repository;
		}

		public GitRepositoryViewModel Repository { get; }
		public string Name => Repository.Name;
		public string Path => Repository.Path;
		public bool HasUpstream { get; set; }

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

		/// <summary>Last per-repo error (or null).</summary>
		public string Status
		{
			get { return status; }
			set { SetProperty(ref status, value); }
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
			StashPushCommand = new DelegateCommand(() => RunSafe(StashPushAsync), () => HasRepository && !IsBusy);
			StashApplyCommand = new DelegateCommand(() => RunSafe(() => StashActionAsync("apply")), () => SelectedStash != null && !IsBusy);
			StashPopCommand = new DelegateCommand(() => RunSafe(() => StashActionAsync("pop")), () => SelectedStash != null && !IsBusy);
			StashDropCommand = new DelegateCommand(() => RunSafe(StashDropAsync), () => SelectedStash != null && !IsBusy);

			CheckoutBranchCommand = new DelegateCommand(() => RunSafe(CheckoutSelectedBranchAsync), () => SelectedBranch != null && !SelectedBranch.IsCurrent && !IsBusy);
			CreateBranchCommand = new DelegateCommand(() => RunSafe(CreateBranchAsync), () => HasRepository && !string.IsNullOrWhiteSpace(NewBranchName) && !IsBusy);
			DeleteBranchCommand = new DelegateCommand(() => RunSafe(DeleteBranchAsync), () => SelectedBranch != null && !SelectedBranch.IsCurrent && !IsBusy);
			RefreshBranchesCommand = new DelegateCommand(() => RunSafe(LoadBranchesAsync), () => HasRepository && !IsBusy);

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

		/// <summary>Selected tab of the tool window (0 = Changes, 1 = Stashes, 2 = History, 3 = Branches, 4 = Sync, 5 = Feed).</summary>
		public int SelectedTabIndex
		{
			get { return selectedTabIndex; }
			set
			{
				if (SetProperty(ref selectedTabIndex, value))
				{
					// lazy-load the multi-repo tabs on first visit
					if (value == syncTabIndex && SyncRows.Count == 0)
						RunSafe(RefreshSyncAsync);
					else if (value == feedTabIndex && FeedCommits.Count == 0)
						RunSafe(RefreshFeedAsync);
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

		/// <summary>Message filter for History and Feed ("git log --grep -i").</summary>
		public string HistoryGrep
		{
			get { return historyGrep; }
			set { SetProperty(ref historyGrep, value); }
		}

		/// <summary>Author filter for History and Feed (ignored while <see cref="OnlyMine"/> is set).</summary>
		public string HistoryAuthor
		{
			get { return historyAuthor; }
			set { SetProperty(ref historyAuthor, value); }
		}

		/// <summary>Filter History and Feed to commits of the configured "git config user.name".</summary>
		public bool OnlyMine
		{
			get { return onlyMine; }
			set { SetProperty(ref onlyMine, value); }
		}

		/// <summary>Index into All/7/30/90 days (History and Feed).</summary>
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

		/// <summary>Selects the repository containing <paramref name="repositoryPath"/> and switches to the History tab.</summary>
		public async Task ShowHistoryAsync(string repositoryPath)
		{
			await LoadAsync();
			var repo = Repositories.FirstOrDefault(r => string.Equals(r.Path, repositoryPath, StringComparison.OrdinalIgnoreCase));
			if (repo == null)
			{
				// repo lives outside the configured working folders — add it on the fly
				repo = new GitRepositoryViewModel(repositoryPath);
				Repositories.Add(repo);
			}
			SelectedTabIndex = 2;
			SelectedRepository = repo;
		}

		/// <summary>Fire-and-forget wrapper: every failure lands in the status line instead of crashing WPF.</summary>
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
			var folders = settings.Get<List<string>>(workingFoldersKey, globalContext) ?? new List<string>();
			var previous = SelectedRepository?.Path;
			var roots = await Task.Run(() => FindRepositoryRoots(folders));

			Repositories.Clear();
			foreach (string root in roots)
				Repositories.Add(new GitRepositoryViewModel(root));

			SelectedRepository = Repositories.FirstOrDefault(r => string.Equals(r.Path, previous, StringComparison.OrdinalIgnoreCase))
								 ?? Repositories.FirstOrDefault();
			if (SelectedRepository == null)
				StatusMessage = "No git repositories found beneath the configured working folders.";

			// multi-repo tabs: reload immediately when visible, otherwise lazily on next visit
			SyncRows.Clear();
			FeedCommits.Clear();
			if (SelectedTabIndex == syncTabIndex)
				await RefreshSyncAsync();
			else if (SelectedTabIndex == feedTabIndex)
				await RefreshFeedAsync();
		}

		private async Task RefreshRepositoryAsync()
		{
			var repo = SelectedRepository;
			if (repo == null)
				return;

			repo.Branch = await git.GetCurrentBranchAsync(repo.Path);

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

		/// <summary>Resolves the effective author filter (OnlyMine wins over the author text box).</summary>
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
				foreach (var commit in await git.GetHistoryAsync(repo.Path, 100, author, SinceDays, grep))
					Commits.Add(commit);
			}
			catch (InvalidOperationException)
			{
				// repository without commits — history stays empty
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

		// ponytail: ahead/behind is one git call per branch, sequential — batch via for-each-ref if repos with many branches feel slow
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
				SyncRows.Add(new RepoSyncViewModel(repo));
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
				row.Status = null;
			}
			catch (Exception e)
			{
				row.Status = e.Message;
			}
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
				default: return git.PushAsync(row.Path, setUpstream: !row.HasUpstream);
			}
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
					// repository without commits — skip
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
					// set the backing field directly: the setter would kick off a concurrent fire-and-forget refresh
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

		/// <summary>All git roots at or beneath the working folders (cheap .git probe, no git.exe).</summary>
		private static List<string> FindRepositoryRoots(IEnumerable<string> workingFolders)
		{
			var roots = new List<string>();
			foreach (string folder in workingFolders.Where(Directory.Exists))
				Scan(folder, 0);
			return roots;

			void Scan(string directory, int depth)
			{
				try
				{
					string dotGit = Path.Combine(directory, ".git");
					if (Directory.Exists(dotGit) || File.Exists(dotGit))
					{
						if (!roots.Contains(directory, StringComparer.OrdinalIgnoreCase))
							roots.Add(directory);
						return; // nested repos below a root are not interesting here
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
	}
}
