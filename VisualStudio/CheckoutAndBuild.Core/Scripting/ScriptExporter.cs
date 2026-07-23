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
			ScriptExportType exportType)
		{
			var included = projects.Where(p => p.IsIncluded).OrderBy(p => p.BuildPriority).ToList();
			var builder = new StringBuilder();

			if (exportType == ScriptExportType.Batch)
				builder.AppendLine("@echo off");
			builder.AppendLine(Comment(exportType, $"CheckoutAndBuild pipeline export {DateTime.Now:yyyy-MM-dd HH:mm}"));

			foreach (var service in services
				.Where(s => s.AllowScriptExport && s.SupportedScriptExportTypes.Contains(exportType))
				.OrderBy(s => s.Order))
			{
				string script = service.GetScript(included, settings, exportType);
				if (string.IsNullOrWhiteSpace(script))
					continue;
				builder.AppendLine();
				builder.AppendLine(Comment(exportType, $"--- {service.OperationName} ---"));
				builder.AppendLine(script.TrimEnd());
			}

			return builder.ToString();
		}

		public static string ExportToFile(IEnumerable<IOperationService> services,
			IReadOnlyList<ISolutionProjectModel> projects,
			IServiceSettings settings,
			ScriptExportType exportType,
			string filePath)
		{
			File.WriteAllText(filePath, Export(services, projects, settings, exportType));
			return filePath;
		}

		private static string Comment(ScriptExportType type, string text) =>
			type == ScriptExportType.Batch ? "rem " + text : "# " + text;
	}
}
