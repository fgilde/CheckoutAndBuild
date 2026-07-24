using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CheckoutAndBuild.Core.Settings
{
	public interface ISettingsService
	{
		T Get<T>(string key, SettingsContext context, T defaultValue = default);
		void Set<T>(string key, SettingsContext context, T value);

		/// <summary>Moves every stored key of <paramref name="oldProfile"/> to <paramref name="newProfile"/> (profile rename keeps its settings).</summary>
		void RenameProfile(string oldProfile, string newProfile);

		void Save();
	}

	/// <summary>
	/// JSON-file backed settings store with profile/repository/branch scoping.
	/// Get falls back branch-scoped → repo-scoped → global → defaultValue.
	/// </summary>
	public sealed class JsonSettingsService : ISettingsService
	{
		private const string keySeparator = "$";

		private readonly string filePath;
		private readonly object syncRoot = new object();
		private readonly Dictionary<string, JsonElement> values;

		public JsonSettingsService(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));
			this.filePath = filePath;
			values = Load(filePath);
		}

		/// <summary>Creates the service on the standard %AppData%\COAB\settings.json location.</summary>
		public static JsonSettingsService CreateDefault()
		{
			string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "COAB");
			return new JsonSettingsService(Path.Combine(directory, "settings.json"));
		}

		public T Get<T>(string key, SettingsContext context, T defaultValue = default)
		{
			lock (syncRoot)
			{
				foreach (string candidate in GetCandidateKeys(key, context))
				{
					if (values.TryGetValue(candidate, out JsonElement element))
					{
						try
						{
							return JsonSerializer.Deserialize<T>(element.GetRawText());
						}
						catch (JsonException)
						{
							return defaultValue;
						}
					}
				}
				return defaultValue;
			}
		}

		public void Set<T>(string key, SettingsContext context, T value)
		{
			lock (syncRoot)
			{
				values[BuildKey(key, context)] = JsonSerializer.SerializeToElement(value);
				SaveCore();
			}
		}

		public void RenameProfile(string oldProfile, string newProfile)
		{
			if (string.IsNullOrEmpty(oldProfile) || string.IsNullOrEmpty(newProfile) || oldProfile == newProfile)
				return;
			lock (syncRoot)
			{
				string oldPrefix = oldProfile + keySeparator;
				foreach (string key in new List<string>(values.Keys))
				{
					if (!key.StartsWith(oldPrefix, StringComparison.Ordinal))
						continue;
					values[newProfile + keySeparator + key.Substring(oldPrefix.Length)] = values[key];
					values.Remove(key);
				}
				SaveCore();
			}
		}

		public void Save()
		{
			lock (syncRoot)
			{
				SaveCore();
			}
		}

		/// <summary>Writes the whole store to a portable .coab/.json file (old settings export).</summary>
		public void ExportTo(string exportFilePath)
		{
			lock (syncRoot)
			{
				File.WriteAllText(exportFilePath, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
			}
		}

		/// <summary>Merges an exported file into this store (imported keys win). Returns the number of imported keys.</summary>
		public int ImportFrom(string importFilePath)
		{
			var imported = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(importFilePath))
						   ?? new Dictionary<string, JsonElement>();
			lock (syncRoot)
			{
				foreach (var pair in imported)
					values[pair.Key] = pair.Value;
				SaveCore();
			}
			return imported.Count;
		}

		/// <summary>Copies every key of one profile onto another (target values are overwritten). Returns the copied key count.</summary>
		public int CopyProfile(string sourceProfile, string targetProfile)
		{
			if (string.IsNullOrEmpty(sourceProfile) || string.IsNullOrEmpty(targetProfile) || sourceProfile == targetProfile)
				return 0;
			lock (syncRoot)
			{
				string sourcePrefix = sourceProfile + keySeparator;
				int copied = 0;
				foreach (string key in new List<string>(values.Keys))
				{
					if (!key.StartsWith(sourcePrefix, StringComparison.Ordinal))
						continue;
					values[targetProfile + keySeparator + key.Substring(sourcePrefix.Length)] = values[key];
					copied++;
				}
				SaveCore();
				return copied;
			}
		}

		/// <summary>Wipes the whole store ("Reset all settings").</summary>
		public void ResetAll()
		{
			lock (syncRoot)
			{
				values.Clear();
				SaveCore();
			}
		}

		/// <summary>Most specific first: branch-scoped, repo-scoped, global.</summary>
		private static IEnumerable<string> GetCandidateKeys(string key, SettingsContext context)
		{
			string profile = GetProfile(context);
			string repositoryPath = context?.RepositoryPath;

			if (!string.IsNullOrEmpty(repositoryPath))
			{
				if (!string.IsNullOrEmpty(context.Branch))
					yield return Combine(profile, repositoryPath, context.Branch, key);
				yield return Combine(profile, repositoryPath, string.Empty, key);
			}
			yield return Combine(profile, string.Empty, string.Empty, key);
		}

		private static string BuildKey(string key, SettingsContext context)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));
			return Combine(GetProfile(context), context?.RepositoryPath ?? string.Empty, context?.Branch ?? string.Empty, key);
		}

		private static string GetProfile(SettingsContext context)
		{
			return string.IsNullOrEmpty(context?.Profile) ? SettingsContext.DefaultProfile : context.Profile;
		}

		private static string Combine(string profile, string repositoryPath, string branch, string key)
		{
			return string.Join(keySeparator, profile, repositoryPath, branch, key);
		}

		private static Dictionary<string, JsonElement> Load(string filePath)
		{
			if (!File.Exists(filePath))
				return new Dictionary<string, JsonElement>();
			try
			{
				string json = File.ReadAllText(filePath);
				return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
					   ?? new Dictionary<string, JsonElement>();
			}
			catch (JsonException)
			{
				// ponytail: corrupt file starts fresh; add backup-on-corruption if user data matters.
				return new Dictionary<string, JsonElement>();
			}
		}

		private void SaveCore()
		{
			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
			File.WriteAllText(filePath, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
		}
	}
}
