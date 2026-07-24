using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Plugins;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.VisualStudio.Settings
{
	/// <summary>
	/// Reflection over the [SettingsProperty]-decorated provider classes plus JSON persistence.
	/// Persistence key: "&lt;SettingsClass&gt;.&lt;Property&gt;". Global values use an empty
	/// <see cref="SettingsContext"/>; solution-specific overrides use RepositoryPath = solution path
	/// (the JsonSettingsService lookup then falls back repo-scoped → global → default automatically,
	/// which is exactly the old "empty override shows the effective global value" behavior).
	/// </summary>
	public static class SettingsUiFactory
	{
		/// <summary>All known settings provider classes (replaces the old MEF GetExportedValues scan).</summary>
		public static readonly Type[] SettingsClasses =
		{
			typeof(CheckoutServiceSettings),
			typeof(CleanServiceSettings),
			typeof(NugetServiceSettings),
			typeof(BuildServiceSettings),
			typeof(UnitTestServiceSettings),
			typeof(MiscellaneousSettings)
		};

		/// <summary>
		/// Built-in settings classes plus every ISettingsProviderClass exported by loaded plugins
		/// (the built-ins are exported from the core assembly too and are de-duplicated).
		/// </summary>
		public static IReadOnlyList<Type> GetSettingsClasses(PluginHost pluginHost)
		{
			var result = new List<Type>(SettingsClasses);
			foreach (var provider in pluginHost?.GetExportedValues<ISettingsProviderClass>() ?? Enumerable.Empty<ISettingsProviderClass>())
			{
				Type type = provider.GetType();
				if (!result.Contains(type))
					result.Add(type);
			}
			return result;
		}

		public static string GetKey(PropertyInfo property) => property.DeclaringType.Name + "." + property.Name;

		public static IEnumerable<PropertyInfo> GetSettingsProperties(Type settingsClass)
		{
			return settingsClass.GetProperties()
				.Where(p => p.CanWrite && p.GetCustomAttribute<SettingsPropertyAttribute>() != null);
		}

		/// <summary>
		/// Properties editable in the given mode: global mode shows Global +
		/// GlobalWithProjectSpecificOverride, solution mode shows ProjectSpecific +
		/// GlobalWithProjectSpecificOverride (matches the old GenerateSettingsObjectForInspector).
		/// </summary>
		public static IEnumerable<PropertyInfo> GetEditableProperties(Type settingsClass, bool projectSpecific)
		{
			return GetSettingsProperties(settingsClass).Where(p =>
			{
				var availability = p.GetCustomAttribute<SettingsPropertyAttribute>().Availability;
				return projectSpecific
					? availability != SettingsAvailability.Global
					: availability != SettingsAvailability.ProjectSpecific;
			});
		}

		/// <summary>Group header: the [Category] of the class' properties, else the class name without the Settings suffix.</summary>
		public static string GetGroupName(Type settingsClass)
		{
			string category = GetSettingsProperties(settingsClass)
				.Select(p => p.GetCustomAttribute<CategoryAttribute>()?.Category)
				.FirstOrDefault(c => !string.IsNullOrEmpty(c));
			if (!string.IsNullOrEmpty(category))
				return category;
			string name = settingsClass.Name;
			foreach (string suffix in new[] { "ServiceSettings", "Settings" })
			{
				if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
					return name.Substring(0, name.Length - suffix.Length);
			}
			return name;
		}

		public static object GetDefaultValue(PropertyInfo property)
		{
			var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
			if (defaultValue != null)
				return defaultValue.Value;
			return property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
		}

		/// <summary>Current value from the store (with the context's scope fallback) or the [DefaultValue].</summary>
		public static object GetValue(ISettingsService settings, SettingsContext context, PropertyInfo property)
		{
			// ISettingsService is generic-only; JsonElement round-trips any stored value untyped.
			JsonElement element = settings.Get<JsonElement>(GetKey(property), context);
			if (element.ValueKind == JsonValueKind.Undefined)
				return GetDefaultValue(property);
			try
			{
				return JsonSerializer.Deserialize(element.GetRawText(), property.PropertyType);
			}
			catch (JsonException)
			{
				return GetDefaultValue(property);
			}
		}

		public static void SetValue(ISettingsService settings, SettingsContext context, PropertyInfo property, object value)
		{
			settings.Set(GetKey(property), context, JsonSerializer.SerializeToElement(value, property.PropertyType));
		}

		/// <summary>
		/// Populated settings instance for the pipeline. With a solution path, ProjectSpecific and
		/// override properties read solution-scoped (falling back to global); Global-only properties
		/// always read the global value. Mirrors the old SettingsService.GetSettingsFromProvider.
		/// </summary>
		public static T CreateSettings<T>(ISettingsService settings, string solutionPath = null, string profile = null)
			where T : ISettingsProviderClass, new()
		{
			profile = string.IsNullOrEmpty(profile) ? SettingsContext.DefaultProfile : profile;
			var globalContext = new SettingsContext { Profile = profile };
			var solutionContext = solutionPath == null ? null : new SettingsContext { Profile = profile, RepositoryPath = solutionPath };
			var instance = new T();
			foreach (PropertyInfo property in GetSettingsProperties(typeof(T)))
			{
				var availability = property.GetCustomAttribute<SettingsPropertyAttribute>().Availability;
				var context = solutionContext != null && availability != SettingsAvailability.Global
					? solutionContext
					: globalContext;
				property.SetValue(instance, GetValue(settings, context, property));
			}
			return instance;
		}
	}
}
