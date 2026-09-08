using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CheckoutAndBuild.VisualStudio.ViewModels;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class GitToolWindowControl : UserControl
	{
		private readonly GitViewModel viewModel = new GitViewModel();

		public GitToolWindowControl()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			InitializeComponent();
			DataContext = viewModel;
			Loaded += async (sender, e) => await viewModel.LoadAsync();
		}

		internal System.Threading.Tasks.Task ShowHistoryAsync(string repositoryPath) => viewModel.ShowHistoryAsync(repositoryPath);

		internal System.Threading.Tasks.Task ShowWorktreesAsync(string repositoryPath) => viewModel.ShowWorktreesAsync(repositoryPath);

		internal System.Threading.Tasks.Task ShowRepositoryAsync(string repositoryPath) => viewModel.ShowRepositoryAsync(repositoryPath);

		/// <summary>Searchable repository picker: a popup with a filter box and the repositories grouped by working folder.</summary>
		private void OnRepoPickerClick(object sender, RoutedEventArgs e)
		{
			var popup = new Popup
			{
				PlacementTarget = repoPickerButton,
				Placement = PlacementMode.Bottom,
				StaysOpen = false,
				AllowsTransparency = true
			};

			var searchBox = new TextBox { Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(2) };
			var list = new StackPanel();
			var scroll = new ScrollViewer
			{
				Content = list,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				MaxHeight = 360
			};
			var panel = new StackPanel { MinWidth = 300 };
			panel.Children.Add(searchBox);
			panel.Children.Add(scroll);
			var border = new System.Windows.Controls.Border { Child = panel, BorderThickness = new Thickness(1), Padding = new Thickness(8) };
			border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty,
				Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundBrushKey);
			border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty,
				Microsoft.VisualStudio.PlatformUI.EnvironmentColors.DropDownBorderBrushKey);
			popup.Child = border;

			void Fill(string filter)
			{
				list.Children.Clear();
				var matching = viewModel.Repositories.Where(r =>
					string.IsNullOrEmpty(filter)
					|| r.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
					|| (r.Folder ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
					|| (r.Branch ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
				foreach (var group in matching.GroupBy(r => r.Folder ?? "", StringComparer.OrdinalIgnoreCase))
				{
					list.Children.Add(new TextBlock
					{
						Text = group.Key,
						FontSize = 10,
						FontWeight = FontWeights.SemiBold,
						Opacity = 0.6,
						Margin = new Thickness(2, 5, 2, 2)
					});
					foreach (var repo in group)
					{
						var label = new TextBlock();
						label.Inlines.Add(new System.Windows.Documents.Run(repo.Name)
						{
							FontWeight = repo == viewModel.SelectedRepository ? FontWeights.Bold : FontWeights.Normal
						});
						if (!string.IsNullOrEmpty(repo.Branch))
							label.Inlines.Add(new System.Windows.Documents.Run($"  ({repo.Branch})")
							{
								FontSize = 11,
								FontStyle = FontStyles.Italic
							});
						var item = new Button
						{
							Style = (Style)FindResource("CoabLinkButton"),
							HorizontalAlignment = HorizontalAlignment.Stretch,
							HorizontalContentAlignment = HorizontalAlignment.Left,
							Margin = new Thickness(8, 1, 2, 1),
							Content = label,
							ToolTip = repo.Path
						};
						var target = repo;
						item.Click += (s, args) =>
						{
							popup.IsOpen = false;
							viewModel.SelectedRepository = target;
						};
						list.Children.Add(item);
					}
				}
				if (list.Children.Count == 0)
					list.Children.Add(new TextBlock { Text = "No matching repository.", Opacity = 0.6, Margin = new Thickness(2, 4, 2, 4) });
			}

			searchBox.TextChanged += (s, args) => Fill(searchBox.Text.Trim());
			searchBox.PreviewKeyDown += (s, args) =>
			{
				if (args.Key == Key.Escape)
				{
					popup.IsOpen = false;
					args.Handled = true;
				}
				else if (args.Key == Key.Enter)
				{
					var first = list.Children.OfType<Button>().FirstOrDefault();
					if (first != null)
					{
						first.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
						args.Handled = true;
					}
				}
			};
			popup.Opened += (s, args) => searchBox.Focus();

			Fill("");
			popup.IsOpen = true;
		}

		private async void OnFeedDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var feedCommit = ((ListBox)sender).SelectedItem as FeedCommitViewModel;
			if (feedCommit != null)
				await viewModel.ShowCommitInHistoryAsync(feedCommit);
		}

		private void OnChangeDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var change = ((ListBox)sender).SelectedItem as ChangeViewModel;
			if (change != null && File.Exists(change.FullPath))
				VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, change.FullPath);
		}
	}
}
