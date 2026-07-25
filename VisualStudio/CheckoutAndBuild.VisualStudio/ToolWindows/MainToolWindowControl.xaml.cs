using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CheckoutAndBuild.VisualStudio.ViewModels;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class MainToolWindowControl : UserControl
	{
		private readonly MainViewModel viewModel = MainViewModel.Shared;
		private DateTime lastServicesPopupClose;

		public MainToolWindowControl()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			InitializeComponent();
			viewModel.ErrorSink = CheckoutAndBuildPackage.Instance?.ErrorListProvider;
			DataContext = viewModel;
			Loaded += async (sender, e) => await viewModel.LoadAsync();
			PreviewKeyDown += OnPreviewKeyDown;
		}

		/// <summary>Ctrl+E focuses the filter box (old SearchBox shortcut); Esc in the box clears it.</summary>
		private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.E
				&& (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
			{
				filterBox.Focus();
				filterBox.SelectAll();
				e.Handled = true;
			}
			else if (e.Key == System.Windows.Input.Key.Escape && filterBox.IsKeyboardFocused)
			{
				filterBox.Clear();
			}
		}

		/// <summary>Opens the global settings view (used by the Tools → Options page).</summary>
		internal void ShowSettings() => viewModel.OpenGlobalSettings();

		/// <summary>
		/// Opens the per-solution services popover. StaysOpen=False closes the popup on the mouse-down
		/// of the very click that should toggle it shut, so a click arriving right after a close is
		/// swallowed instead of instantly reopening (old ProjectViewModel.canOpenPopup behavior).
		/// </summary>
		private void OnServicesLinkClick(object sender, RoutedEventArgs e)
		{
			if (!((sender as FrameworkElement)?.Tag is Popup popup))
				return;
			if ((DateTime.UtcNow - lastServicesPopupClose).TotalMilliseconds < 250)
				return;
			popup.Closed -= OnServicesPopupClosed;
			popup.Closed += OnServicesPopupClosed;
			popup.IsOpen = true;
		}

		private void OnServicesPopupClosed(object sender, EventArgs e) => lastServicesPopupClose = DateTime.UtcNow;

		/// <summary>Branch link of a single repository (inline mode, up to 3 repos per folder).</summary>
		private async void OnBranchLinkClick(object sender, RoutedEventArgs e)
		{
			if (!((sender as FrameworkElement)?.DataContext is RepositoryBranchViewModel repository))
				return;
			var popup = CreateThemedPopup((Button)sender, out Border border);
			var panel = await BuildBranchPanelAsync(repository, popup, goBack: null);
			if (panel == null)
				return;
			border.Child = panel;
			popup.IsOpen = true;
		}

		/// <summary>
		/// Summary button when a folder has many repositories: popup with a searchable repo list;
		/// clicking a repo swaps in its branch panel (with a back link).
		/// </summary>
		private void OnRepositoriesLinkClick(object sender, RoutedEventArgs e)
		{
			if (!((sender as FrameworkElement)?.DataContext is WorkingFolderViewModel folder))
				return;
			var popup = CreateThemedPopup((Button)sender, out Border border);

			var searchBox = new TextBox { Margin = new Thickness(6, 6, 6, 4), FontSize = 11 };
			var list = new ListBox
			{
				Margin = new Thickness(2, 0, 2, 4),
				MaxHeight = 340,
				BorderThickness = new Thickness(0),
				Background = System.Windows.Media.Brushes.Transparent
			};
			ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
			foreach (var repository in folder.Repositories)
			{
				var row = new DockPanel { LastChildFill = false, MinWidth = 260 };
				var name = new TextBlock
				{
					Text = repository.RepositoryName,
					FontWeight = FontWeights.SemiBold,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxWidth = 180
				};
				var branch = new TextBlock
				{
					Text = repository.CurrentBranch,
					Margin = new Thickness(8, 0, 0, 0),
					Opacity = 0.75,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxWidth = 160
				};
				DockPanel.SetDock(name, Dock.Left);
				DockPanel.SetDock(branch, Dock.Right);
				row.Children.Add(name);
				row.Children.Add(branch);
				list.Items.Add(new ListBoxItem
				{
					Content = row,
					Tag = repository,
					ToolTip = repository.RepositoryPath
				});
			}
			searchBox.TextChanged += (s, args) =>
			{
				string filter = searchBox.Text;
				foreach (ListBoxItem item in list.Items)
				{
					var repository = (RepositoryBranchViewModel)item.Tag;
					item.Visibility = repository.RepositoryName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
						|| (repository.CurrentBranch?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
						? Visibility.Visible
						: Visibility.Collapsed;
				}
			};

			var repoPanel = new StackPanel { MinWidth = 260, MaxWidth = 360 };
			repoPanel.Children.Add(searchBox);
			repoPanel.Children.Add(list);
			border.Child = repoPanel;

			async void OpenRepository(RepositoryBranchViewModel repository)
			{
				var branchPanel = await BuildBranchPanelAsync(repository, popup, goBack: () =>
				{
					border.Child = repoPanel;
					list.SelectedItem = null;
					searchBox.Focus();
				});
				if (branchPanel != null && popup.IsOpen)
					border.Child = branchPanel;
			}
			list.PreviewMouseLeftButtonUp += (s, args) =>
			{
				if (list.SelectedItem is ListBoxItem selected)
					OpenRepository((RepositoryBranchViewModel)selected.Tag);
			};
			searchBox.KeyDown += (s, args) =>
			{
				if (args.Key != System.Windows.Input.Key.Enter)
					return;
				var first = list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Visibility == Visibility.Visible);
				if (first != null)
					OpenRepository((RepositoryBranchViewModel)first.Tag);
			};

			popup.Opened += (s, args) => searchBox.Focus();
			popup.IsOpen = true;
		}

		private static System.Windows.Controls.Primitives.Popup CreateThemedPopup(Button target, out Border border)
		{
			var popup = new System.Windows.Controls.Primitives.Popup
			{
				PlacementTarget = target,
				Placement = PlacementMode.Bottom,
				StaysOpen = false,
				AllowsTransparency = true,
				PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Slide
			};
			border = new Border { BorderThickness = new Thickness(1) };
			border.SetResourceReference(Border.BackgroundProperty,
				Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarMenuBackgroundGradientBrushKey);
			border.SetResourceReference(Border.BorderBrushProperty,
				Microsoft.VisualStudio.PlatformUI.EnvironmentColors.DropDownBorderBrushKey);
			popup.Child = border;
			return popup;
		}

		/// <summary>
		/// Branch panel of one repository: filter box (Enter = first match), scrolling branch list
		/// and the worktrees section. Long names are trimmed with the full name in the tooltip.
		/// </summary>
		private async System.Threading.Tasks.Task<FrameworkElement> BuildBranchPanelAsync(
			RepositoryBranchViewModel repository, System.Windows.Controls.Primitives.Popup popup, Action goBack)
		{
			var branches = await repository.GetBranchesAsync();
			if (branches.Count == 0)
				return null;
			IReadOnlyList<CheckoutAndBuild.Core.Git.GitWorktree> worktrees;
			try
			{
				worktrees = await new CheckoutAndBuild.Core.Git.GitService().GetWorktreesAsync(repository.RepositoryPath);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild worktree list failed: " + ex.Message);
				worktrees = new CheckoutAndBuild.Core.Git.GitWorktree[0];
			}

			TextBlock BranchText(string text) => new TextBlock
			{
				Text = text,
				TextTrimming = TextTrimming.CharacterEllipsis,
				MaxWidth = 300,
				ToolTip = text
			};

			var panel = new StackPanel { MinWidth = 220, MaxWidth = 340 };

			// header: back link (multi-repo mode) + repo name
			if (goBack != null)
			{
				var headerRow = new DockPanel { Margin = new Thickness(6, 6, 6, 0) };
				var back = new Button
				{
					Style = (Style)FindResource("CoabLinkButton"),
					Content = "← Back",
					FontSize = 11
				};
				back.Click += (s, args) => goBack();
				var repoName = new TextBlock
				{
					Text = repository.RepositoryName,
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(8, 0, 0, 0),
					VerticalAlignment = VerticalAlignment.Center,
					TextTrimming = TextTrimming.CharacterEllipsis
				};
				DockPanel.SetDock(back, Dock.Left);
				headerRow.Children.Add(back);
				headerRow.Children.Add(repoName);
				panel.Children.Add(headerRow);
			}

			var searchBox = new TextBox { Margin = new Thickness(6, 6, 6, 4), FontSize = 11 };
			var list = new ListBox
			{
				Margin = new Thickness(2, 0, 2, 4),
				MaxHeight = 300,
				BorderThickness = new Thickness(0),
				Background = System.Windows.Media.Brushes.Transparent
			};
			ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
			foreach (string branch in branches)
			{
				var text = BranchText(branch);
				if (branch == repository.CurrentBranch)
					text.FontWeight = FontWeights.Bold;
				list.Items.Add(new ListBoxItem { Content = text, Tag = branch });
			}
			searchBox.TextChanged += (s, args) =>
			{
				string filter = searchBox.Text;
				foreach (ListBoxItem item in list.Items)
					item.Visibility = ((string)item.Tag).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
						? Visibility.Visible
						: Visibility.Collapsed;
			};
			async void CheckoutBranch(string branch)
			{
				popup.IsOpen = false;
				await repository.CheckoutAsync(branch);
			}
			list.PreviewMouseLeftButtonUp += (s, args) =>
			{
				if (list.SelectedItem is ListBoxItem selected)
					CheckoutBranch((string)selected.Tag);
			};
			searchBox.KeyDown += (s, args) =>
			{
				if (args.Key != System.Windows.Input.Key.Enter)
					return;
				var first = list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Visibility == Visibility.Visible);
				if (first != null)
					CheckoutBranch((string)first.Tag);
			};

			panel.Children.Add(searchBox);
			panel.Children.Add(list);

			// worktrees section
			if (worktrees.Count > 0)
			{
				var separator = new Border { Height = 1, Margin = new Thickness(4, 2, 4, 4) };
				separator.SetResourceReference(Border.BackgroundProperty,
					Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarToolBarSeparatorBrushKey);
				panel.Children.Add(separator);
				panel.Children.Add(new TextBlock
				{
					Text = "Worktrees",
					FontWeight = FontWeights.SemiBold,
					FontSize = 11,
					Margin = new Thickness(8, 0, 8, 2),
					Opacity = 0.7
				});
				foreach (var worktree in worktrees)
				{
					bool isCurrent = string.Equals(worktree.Path, repository.RepositoryPath, StringComparison.OrdinalIgnoreCase);
					string label = $"{(isCurrent ? "● " : "")}{worktree.Name}  ({(worktree.IsDetached ? "detached" : worktree.Branch)})";
					var item = new Button
					{
						Style = (Style)FindResource("CoabLinkButton"),
						HorizontalAlignment = HorizontalAlignment.Left,
						Margin = new Thickness(8, 1, 8, 1),
						FontSize = 11,
						Content = BranchText(label),
						ToolTip = isCurrent ? worktree.Path : worktree.Path + "\nClick: add as working folder",
						IsEnabled = !isCurrent
					};
					var target = worktree;
					item.Click += (s, args) =>
					{
						popup.IsOpen = false;
						MainViewModel.Shared.AddFolderByPath(target.Path);
					};
					panel.Children.Add(item);
				}
				var manage = new Button
				{
					Style = (Style)FindResource("CoabLinkButton"),
					HorizontalAlignment = HorizontalAlignment.Left,
					Margin = new Thickness(8, 3, 8, 6),
					FontSize = 11,
					Content = "Manage Worktrees…"
				};
				manage.Click += (s, args) =>
				{
					popup.IsOpen = false;
					CheckoutAndBuildPackage.Instance?.ShowGitWorktrees(repository.RepositoryPath);
				};
				panel.Children.Add(manage);
			}

			panel.Loaded += (s, args) => searchBox.Focus();
			return panel;
		}

		/// <summary>Enter in the priority box applies the value immediately (binding updates on focus loss).</summary>
		private void OnPriorityBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key != System.Windows.Input.Key.Enter)
				return;
			var box = (TextBox)sender;
			box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
			System.Windows.Input.Keyboard.ClearFocus();
			e.Handled = true;
		}

		/// <summary>Opens the "More" drop-down (context menu) below the toolbar button.</summary>
		private void OnMoreClick(object sender, RoutedEventArgs e)
		{
			var button = (Button)sender;
			button.ContextMenu.PlacementTarget = button;
			button.ContextMenu.Placement = PlacementMode.Bottom;
			button.ContextMenu.IsOpen = true;
		}
	}
}
