using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;

namespace CheckoutAndBuild.VisualStudio.Settings
{
	/// <summary>
	/// Model of the dynamic settings editor. Global mode (solutionPath == null) shows Global +
	/// GlobalWithProjectSpecificOverride properties with global values; solution mode shows
	/// ProjectSpecific + override properties solution-scoped (an unchanged value displays the
	/// effective global value; editing writes a solution override).
	/// </summary>
	public class SettingsViewModel
	{
		public SettingsViewModel(ISettingsService settings, string title, string solutionPath, Action close,
			IEnumerable<Type> settingsClasses = null, string profile = null)
		{
			Title = title;
			IsProjectSpecific = solutionPath != null;
			CloseCommand = new DelegateCommand(close ?? (() => { }));

			var context = new SettingsContext { RepositoryPath = solutionPath };
			if (!string.IsNullOrEmpty(profile))
				context.Profile = profile;
			foreach (Type settingsClass in settingsClasses ?? SettingsUiFactory.SettingsClasses)
			{
				var entries = SettingsUiFactory.GetEditableProperties(settingsClass, IsProjectSpecific)
					.Select(p => SettingEntryViewModel.Create(p, settings, context))
					.ToList();
				if (entries.Count > 0)
					Groups.Add(new SettingsGroupViewModel(SettingsUiFactory.GetGroupName(settingsClass), entries));
			}
		}

		public string Title { get; }

		public bool IsProjectSpecific { get; }

		public ICommand CloseCommand { get; }

		public ObservableCollection<SettingsGroupViewModel> Groups { get; } = new ObservableCollection<SettingsGroupViewModel>();
	}

	/// <summary>One expander in the settings view (one settings provider class).</summary>
	public class SettingsGroupViewModel
	{
		public SettingsGroupViewModel(string name, IReadOnlyList<SettingEntryViewModel> entries)
		{
			Name = name;
			Entries = entries;
		}

		public string Name { get; }

		public IReadOnlyList<SettingEntryViewModel> Entries { get; }
	}

	/// <summary>One [SettingsProperty] entry. Subclasses pick the editor via implicit DataTemplates.</summary>
	public abstract class SettingEntryViewModel : NotificationObject
	{
		private readonly ISettingsService settings;
		private readonly SettingsContext context;

		protected SettingEntryViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
		{
			Property = property;
			this.settings = settings;
			this.context = context;
			var attribute = property.GetCustomAttribute<SettingsPropertyAttribute>();
			DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? attribute.Name;
			Description = attribute.Description;
		}

		protected PropertyInfo Property { get; }

		public string DisplayName { get; }

		public string Description { get; }

		protected object LoadValue() => SettingsUiFactory.GetValue(settings, context, Property);

		protected void SaveValue(object value) => SettingsUiFactory.SetValue(settings, context, Property, value);

		public static SettingEntryViewModel Create(PropertyInfo property, ISettingsService settings, SettingsContext context)
		{
			Type type = property.PropertyType;
			if (type == typeof(bool))
				return new BoolSettingViewModel(property, settings, context);
			if (type.IsEnum)
				return new EnumSettingViewModel(property, settings, context);
			if (type == typeof(int))
				return new IntSettingViewModel(property, settings, context);
			if (type == typeof(string[]))
				return new StringArraySettingViewModel(property, settings, context);
			// ponytail: string is the fallback; add editors when a settings class introduces new types.
			return new StringSettingViewModel(property, settings, context);
		}
	}

	public sealed class BoolSettingViewModel : SettingEntryViewModel
	{
		private bool value;

		public BoolSettingViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
			: base(property, settings, context)
		{
			value = (bool)LoadValue();
		}

		public bool Value
		{
			get { return value; }
			set { if (SetProperty(ref this.value, value)) SaveValue(value); }
		}
	}

	public sealed class EnumSettingViewModel : SettingEntryViewModel
	{
		private object value;

		public EnumSettingViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
			: base(property, settings, context)
		{
			Values = Enum.GetValues(property.PropertyType);
			value = LoadValue();
		}

		public Array Values { get; }

		public object Value
		{
			get { return value; }
			set { if (SetProperty(ref this.value, value)) SaveValue(value); }
		}
	}

	public sealed class IntSettingViewModel : SettingEntryViewModel
	{
		private string text;
		private bool hasError;

		public IntSettingViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
			: base(property, settings, context)
		{
			text = LoadValue()?.ToString() ?? "0";
		}

		public string Text
		{
			get { return text; }
			set
			{
				if (!SetProperty(ref text, value))
					return;
				int parsed;
				HasError = !int.TryParse(value, out parsed);
				if (!HasError)
					SaveValue(parsed);
			}
		}

		public bool HasError
		{
			get { return hasError; }
			private set { SetProperty(ref hasError, value); }
		}
	}

	public sealed class StringSettingViewModel : SettingEntryViewModel
	{
		private string value;

		public StringSettingViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
			: base(property, settings, context)
		{
			value = LoadValue() as string ?? string.Empty;
			BrowseCommand = new DelegateCommand(Browse);
		}

		public string Value
		{
			get { return value; }
			set { if (SetProperty(ref this.value, value ?? string.Empty)) SaveValue(this.value); }
		}

		/// <summary>Show a "…" browse button for path-like properties (name contains Path/File/Location).</summary>
		public bool ShowBrowse => NameContains("Path") || NameContains("File") || NameContains("Location");

		public ICommand BrowseCommand { get; }

		private void Browse()
		{
			// File dialog for file-like names, folder browser for plain paths.
			if (NameContains("File") || NameContains("Location") || NameContains("Exe"))
			{
				var dialog = new Microsoft.Win32.OpenFileDialog { Title = DisplayName, CheckFileExists = true };
				if (dialog.ShowDialog() == true)
					Value = dialog.FileName;
			}
			else
			{
				using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = DisplayName, SelectedPath = Value })
				{
					if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
						Value = dialog.SelectedPath;
				}
			}
		}

		private bool NameContains(string part) => Property.Name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>string[] edited as a multi-line textbox, one entry per line.</summary>
	public sealed class StringArraySettingViewModel : SettingEntryViewModel
	{
		private string text;

		public StringArraySettingViewModel(PropertyInfo property, ISettingsService settings, SettingsContext context)
			: base(property, settings, context)
		{
			text = string.Join(Environment.NewLine, LoadValue() as string[] ?? new string[0]);
		}

		public string Text
		{
			get { return text; }
			set
			{
				if (SetProperty(ref text, value ?? string.Empty))
					SaveValue(text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
			}
		}
	}
}
