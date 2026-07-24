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

	/// <summary>Root view model of the "CheckoutAndBuild Git" tool window.</summary>
	public class GitViewModel : NotificationObject
	{
		private const string workingFoldersKey = "WorkingFolders";
		private const int maxScanDepth = 3;
		private static readonly string[] skippedDirectories = { ".vs", "bin", "obj", "node_modules", "packages" };

		private readonly GitService git = new GitService();
		private readonly ISettingsService settings;
		private readonly SettingsContext globalContext = new SettingsContext();

		private GitRepositoryViewModel selectedRepository;
		private ChangeViewModel selectedChange;
		private StashViewModel selectedStash;
		private string diffText;
		private string stashDiffText;
		private string newStashMessage;
		private bool isBusy;
		private bool loadStarted;
		private string lastError;
		private string statusMessage;

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
		}

		public ObservableCollection<GitRepositoryViewModel> Repositories { get; } = new ObservableCollection<GitRepositoryViewModel>();
		public ObservableCollection<ChangeViewModel> Changes { get; } = new ObservableCollection<ChangeViewModel>();
		public ObservableCollection<StashViewModel> Stashes { get; } = new ObservableCollection<StashViewModel>();

		public ICommand RefreshCommand { get; }
		public ICommand ExportPatchCommand { get; }
		public ICommand ExportZipCommand { get; }
		public ICommand StashPushCommand { get; }
		public ICommand StashApplyCommand { get; }
		public ICommand StashPopCommand { get; }
		public ICommand StashDropCommand { get; }

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

		public string DiffText
		{
			get { return diffText; }
			private set { SetProperty(ref diffText, value); }
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
			if (loadStarted)
				return Task.CompletedTask;
			loadStarted = true;
			return GuardedAsync(RefreshAllAsync);
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
