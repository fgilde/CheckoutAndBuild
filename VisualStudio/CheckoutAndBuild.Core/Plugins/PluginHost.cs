using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;

namespace CheckoutAndBuild.Core.Plugins
{
	/// <summary>
	/// MEF plugin loading (port of CheckoutAndBuild2Package.InitCatalog): the core assembly plus all
	/// *.dll files found in the given plugin directories (each directory plus one sub-directory level).
	/// Held singleton-like by the host, but a plain testable class.
	/// </summary>
	public sealed class PluginHost
	{
		private readonly List<string> errors = new List<string>();
		private CompositionContainer container;

		/// <summary>Per-DLL load and per-plugin init failures collected during <see cref="LoadAsync"/>; never thrown.</summary>
		public IReadOnlyList<string> Errors => errors;

		public bool IsLoaded => container != null;

		/// <summary>
		/// Builds the MEF container from the core assembly, all plugin DLLs beneath
		/// <paramref name="pluginDirectories"/> and any <paramref name="additionalAssemblies"/> (used by tests),
		/// then calls <see cref="ICheckoutAndBuildPlugin.Init"/> on every discovered plugin.
		/// </summary>
		/// <returns>The collected error list (same instance as <see cref="Errors"/>).</returns>
		public async Task<IReadOnlyList<string>> LoadAsync(IEnumerable<string> pluginDirectories,
			IServiceProvider hostServices, params Assembly[] additionalAssemblies)
		{
			var catalog = new AggregateCatalog(new AssemblyCatalog(typeof(PluginHost).Assembly));
			foreach (var assembly in additionalAssemblies ?? new Assembly[0])
				catalog.Catalogs.Add(new AssemblyCatalog(assembly));

			var directories = (pluginDirectories ?? Enumerable.Empty<string>()).Where(Directory.Exists).ToList();
			foreach (string dll in directories
				.SelectMany(dir => new[] { dir }.Concat(SafeSubDirectories(dir)))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.SelectMany(dir => Directory.EnumerateFiles(dir, "*.dll")))
			{
				try
				{
					var assemblyCatalog = new AssemblyCatalog(dll);
					assemblyCatalog.Parts.ToList(); // force assembly load: a broken DLL fails here, not at composition
					catalog.Catalogs.Add(assemblyCatalog);
				}
				catch (Exception e)
				{
					errors.Add($"{dll}: {Unwrap(e)}");
				}
			}

			container = new CompositionContainer(catalog, isThreadSafe: true);

			foreach (var plugin in GetExportedValues<ICheckoutAndBuildPlugin>())
			{
				try
				{
					string pluginDirectory = SafeAssemblyDirectory(plugin) ?? directories.FirstOrDefault() ?? string.Empty;
					await plugin.Init(hostServices, pluginDirectory).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					errors.Add($"{plugin.GetType().FullName}.Init: {Unwrap(e)}");
				}
			}
			return errors;
		}

		/// <summary>All exports of <typeparamref name="T"/>; empty (never throws) when not loaded or composition fails.</summary>
		public IEnumerable<T> GetExportedValues<T>()
		{
			if (container == null)
				return Enumerable.Empty<T>();
			try
			{
				return container.GetExportedValues<T>().ToList();
			}
			catch (Exception e)
			{
				errors.Add($"GetExportedValues<{typeof(T).Name}>: {Unwrap(e)}");
				return Enumerable.Empty<T>();
			}
		}

		private static IEnumerable<string> SafeSubDirectories(string directory)
		{
			try
			{
				return Directory.EnumerateDirectories(directory);
			}
			catch (Exception)
			{
				return Enumerable.Empty<string>();
			}
		}

		private static string SafeAssemblyDirectory(object plugin)
		{
			try
			{
				string location = plugin.GetType().Assembly.Location;
				return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static string Unwrap(Exception e)
		{
			if (e is ReflectionTypeLoadException loadException && loadException.LoaderExceptions?.Length > 0)
				return loadException.LoaderExceptions[0]?.Message ?? e.Message;
			return e.Message;
		}
	}
}
