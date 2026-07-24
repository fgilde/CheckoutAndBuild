using System.Collections.Generic;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Settings;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>
	/// IServiceSettings backed by the JSON settings store. Provider classes are populated from the
	/// persisted [SettingsProperty] values (global, or merged with solution-specific overrides).
	/// </summary>
	public sealed class ServiceSettingsAdapter : IServiceSettings
	{
		private readonly ISettingsService settings;
		private readonly SettingsContext context;

		public ServiceSettingsAdapter(ISettingsService settings, SettingsContext context)
		{
			this.settings = settings;
			this.context = context;
		}

		public bool RunPreScriptsAsync => false;

		public bool RunPostScriptsAsync => false;

		public string DelphiPath => settings.Get<string>("DelphiPath", context);

		public string PreBuildScriptPath => settings.Get<string>("PreBuildScript", context);

		public string PostBuildScriptPath => settings.Get<string>("PostBuildScript", context);

		public IDictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();

		public LoggerVerbosity LogLevel => LoggerVerbosity.Minimal;

		public T GetSettingsFromProvider<T>() where T : ISettingsProviderClass, new()
			=> SettingsUiFactory.CreateSettings<T>(settings);

		public T GetSettingsFromProvider<T>(ISolutionProjectModel solutionProject) where T : ISettingsProviderClass, new()
			=> SettingsUiFactory.CreateSettings<T>(settings, solutionProject?.ItemPath);
	}
}
