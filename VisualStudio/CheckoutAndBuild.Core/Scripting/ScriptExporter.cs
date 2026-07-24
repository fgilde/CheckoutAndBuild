using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Scripting
{
	/// <summary>
	/// Exports the configured pipeline as a standalone .bat or .ps1 script
	/// (port of ScriptExportProvider; the per-service commands come from IOperationService.GetScript).
	/// </summary>
	public static class ScriptExporter
	{
		public static string Export(IEnumerable<IOperationService> services,
			IReadOnlyList<ISolutionProjectModel> projects,
			IServiceSettings settings,
			ScriptExportType exportType,
			IEnumerable<IScriptGenerator> scriptGenerators = null)
		{
			var included = projects.Where(p => p.IsIncluded).OrderBy(p => p.BuildPriority).ToList();
			var generators = (scriptGenerators ?? Enumerable.Empty<IScriptGenerator>()).ToList();
			var builder = new StringBuilder();

			if (exportType == ScriptExportType.Batch)
				builder.AppendLine("@echo off");
			builder.AppendLine(Comment(exportType, $"CheckoutAndBuild pipeline export {DateTime.Now:yyyy-MM-dd HH:mm}"));

			foreach (var service in services
				.Where(s => s.AllowScriptExport && s.SupportedScriptExportTypes.Contains(exportType))
				.OrderBy(s => s.Order))
			{
				string script = service.GetScript(included, settings, exportType);
				string pre = Concat(generators.Select(g => g.GeneratePreScriptCode(service, included, settings, exportType)));
				string post = Concat(generators.Select(g => g.GeneratePostScriptCode(service, included, settings, exportType)));
				if (string.IsNullOrWhiteSpace(script) && string.IsNullOrWhiteSpace(pre) && string.IsNullOrWhiteSpace(post))
					continue;
				builder.AppendLine();
				builder.AppendLine(Comment(exportType, $"--- {service.OperationName} ---"));
				if (!string.IsNullOrWhiteSpace(pre))
					builder.AppendLine(pre);
				if (!string.IsNullOrWhiteSpace(script))
					builder.AppendLine(script.TrimEnd());
				if (!string.IsNullOrWhiteSpace(post))
					builder.AppendLine(post);
			}

			return builder.ToString();
		}

		public static string ExportToFile(IEnumerable<IOperationService> services,
			IReadOnlyList<ISolutionProjectModel> projects,
			IServiceSettings settings,
			ScriptExportType exportType,
			string filePath,
			IEnumerable<IScriptGenerator> scriptGenerators = null)
		{
			File.WriteAllText(filePath, Export(services, projects, settings, exportType, scriptGenerators));
			return filePath;
		}

		private static string Concat(IEnumerable<string> parts)
		{
			return string.Join(Environment.NewLine,
				parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.TrimEnd()));
		}

		private static string Comment(ScriptExportType type, string text) =>
			type == ScriptExportType.Batch ? "rem " + text : "# " + text;
	}
}
