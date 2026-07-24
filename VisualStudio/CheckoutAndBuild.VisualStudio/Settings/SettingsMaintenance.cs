using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;

namespace CheckoutAndBuild.VisualStudio.Settings
{
	/// <summary>
	/// Export/Import/Copy/Reset of the settings store (old "Copy/Export Settings" options page),
	/// shown at the bottom of the global settings view.
	/// </summary>
	public class MaintenanceViewModel : NotificationObject
	{
		private readonly JsonSettingsService settings;
		private readonly Func<IReadOnlyList<string>> getProfiles;
		private readonly Func<string> getCurrentProfile;
		private readonly Action afterStoreChanged;
		private string status;

		public MaintenanceViewModel(JsonSettingsService settings, Func<IReadOnlyList<string>> getProfiles,
			Func<string> getCurrentProfile, Action afterStoreChanged)
		{
			this.settings = settings;
			this.getProfiles = getProfiles;
			this.getCurrentProfile = getCurrentProfile;
			this.afterStoreChanged = afterStoreChanged;

			ExportCommand = new DelegateCommand(Export);
			ImportCommand = new DelegateCommand(Import);
			CopyProfileCommand = new DelegateCommand(CopyProfile, () => getProfiles().Count > 1);
			ResetAllCommand = new DelegateCommand(ResetAll);
		}

		public ICommand ExportCommand { get; }
		public ICommand ImportCommand { get; }
		public ICommand CopyProfileCommand { get; }
		public ICommand ResetAllCommand { get; }

		public string Status
		{
			get { return status; }
			private set { SetProperty(ref status, value); }
		}

		private void Export()
		{
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "CheckoutAndBuild settings|*.coab|JSON|*.json",
				FileName = "CheckoutAndBuildSettings.coab"
			};
			if (dialog.ShowDialog() != true)
				return;
			try
			{
				settings.ExportTo(dialog.FileName);
				Status = "Exported: " + dialog.FileName;
			}
			catch (Exception e)
			{
				Status = e.Message;
			}
		}

		private void Import()
		{
			var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CheckoutAndBuild settings|*.coab;*.json" };
			if (dialog.ShowDialog() != true)
				return;
			if (MessageBox.Show("Merge the imported settings into the current store? Imported values overwrite existing ones.",
					"CheckoutAndBuild", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
			try
			{
				int count = settings.ImportFrom(dialog.FileName);
				Status = $"Imported {count} value(s).";
				afterStoreChanged?.Invoke();
			}
			catch (Exception e)
			{
				Status = e.Message;
			}
		}

		private void CopyProfile()
		{
			var profiles = getProfiles();
			var source = new System.Windows.Controls.ComboBox { ItemsSource = profiles, SelectedItem = getCurrentProfile(), Margin = new Thickness(8, 2, 8, 4) };
			var target = new System.Windows.Controls.ComboBox { ItemsSource = profiles, Margin = new Thickness(8, 2, 8, 4) };
			target.SelectedItem = profiles.FirstOrDefault(p => p != getCurrentProfile());
			var ok = new System.Windows.Controls.Button { Content = "Copy", Width = 72, Margin = new Thickness(0, 8, 8, 8), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
			var panel = new System.Windows.Controls.StackPanel();
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Copy all settings from profile:", Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7 });
			panel.Children.Add(source);
			panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "to profile (existing values are overwritten):", Margin = new Thickness(8, 4, 8, 0), Opacity = 0.7 });
			panel.Children.Add(target);
			panel.Children.Add(ok);

			var window = new Window
			{
				Title = "Copy Profile Settings",
				Content = panel,
				Width = 360,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			ok.Click += (s, e) => window.DialogResult = true;
			if (window.ShowDialog() != true)
				return;

			string from = source.SelectedItem as string;
			string to = target.SelectedItem as string;
			if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
			{
				Status = "Source and target profile must differ.";
				return;
			}
			int copied = settings.CopyProfile(from, to);
			Status = $"Copied {copied} value(s) from '{from}' to '{to}'.";
			afterStoreChanged?.Invoke();
		}

		private void ResetAll()
		{
			if (MessageBox.Show("Reset ALL CheckoutAndBuild settings? This cannot be undone.",
					"CheckoutAndBuild", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
				return;
			settings.ResetAll();
			Status = "All settings were reset.";
			afterStoreChanged?.Invoke();
		}
	}

	/// <summary>
	/// Plugin management (old "Plugins / Extensions" options page): lists the extension's
	/// Plugins directory, installs from .zip/.vsix, removes entries. Changes need a VS restart.
	/// </summary>
	public class PluginsViewModel : NotificationObject
	{
		private readonly IReadOnlyList<string> loadErrors;
		private string status;

		public PluginsViewModel(IReadOnlyList<string> loadErrors)
		{
			this.loadErrors = loadErrors ?? new string[0];
			InstallCommand = new DelegateCommand(Install);
			RemoveCommand = new DelegateCommand(p => Remove(p as string), p => p is string);
			OpenFolderCommand = new DelegateCommand(() => System.Diagnostics.Process.Start("explorer.exe", $"\"{PluginsDirectory}\""));
			RefreshItems();
		}

		public static string PluginsDirectory
		{
			get
			{
				string extensionDir = Path.GetDirectoryName(typeof(PluginsViewModel).Assembly.Location);
				return Path.Combine(extensionDir ?? ".", "Plugins");
			}
		}

		public ObservableCollection<string> Items { get; } = new ObservableCollection<string>();

		public string LoadErrors => string.Join(Environment.NewLine, loadErrors);

		public bool HasLoadErrors => loadErrors.Count > 0;

		public ICommand InstallCommand { get; }
		public ICommand RemoveCommand { get; }
		public ICommand OpenFolderCommand { get; }

		public string Status
		{
			get { return status; }
			private set { SetProperty(ref status, value); }
		}

		private void RefreshItems()
		{
			Items.Clear();
			if (!Directory.Exists(PluginsDirectory))
				return;
			foreach (string directory in Directory.EnumerateDirectories(PluginsDirectory))
				Items.Add(Path.GetFileName(directory));
			foreach (string dll in Directory.EnumerateFiles(PluginsDirectory, "*.dll"))
				Items.Add(Path.GetFileName(dll));
		}

		private void Install()
		{
			var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Plugin package|*.zip;*.vsix|Plugin assembly|*.dll" };
			if (dialog.ShowDialog() != true)
				return;
			try
			{
				Directory.CreateDirectory(PluginsDirectory);
				if (dialog.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(dialog.FileName, Path.Combine(PluginsDirectory, Path.GetFileName(dialog.FileName)), overwrite: true);
				}
				else
				{
					string targetDir = Path.Combine(PluginsDirectory, Path.GetFileNameWithoutExtension(dialog.FileName));
					if (Directory.Exists(targetDir))
						Directory.Delete(targetDir, true);
					ZipFile.ExtractToDirectory(dialog.FileName, targetDir);
				}
				RefreshItems();
				Status = "Installed. Please restart Visual Studio to load the plugin.";
			}
			catch (Exception e)
			{
				Status = e.Message;
			}
		}

		private void Remove(string item)
		{
			if (item == null)
				return;
			if (MessageBox.Show($"Remove plugin '{item}'?", "CheckoutAndBuild",
					MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
			try
			{
				string path = Path.Combine(PluginsDirectory, item);
				if (Directory.Exists(path))
					Directory.Delete(path, true);
				else if (File.Exists(path))
					File.Delete(path);
				RefreshItems();
				Status = "Removed. Please restart Visual Studio to unload the plugin.";
			}
			catch (Exception e)
			{
				Status = e.Message;
			}
		}
	}
}
