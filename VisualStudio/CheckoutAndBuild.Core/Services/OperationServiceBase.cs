using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Pipeline;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>
	/// Common cancel bookkeeping for the operation services (ports the old BaseService semantics).
	/// MEF export happens via [InheritedExport] on <see cref="IOperationService"/>.
	/// </summary>
	public abstract class OperationServiceBase : IOperationService
	{
		private ConcurrentDictionary<ISolutionProjectModel, bool> cancelledSolutions =
			new ConcurrentDictionary<ISolutionProjectModel, bool>();

		protected PausableCancellationTokenSource Cancellation { get; private set; }

		public abstract int Order { get; }
		public abstract Guid ServiceId { get; }
		public abstract string OperationName { get; }

		public virtual bool AllowScriptExport => true;

		public virtual ScriptExportType[] SupportedScriptExportTypes =>
			new[] { ScriptExportType.Batch, ScriptExportType.Powershell };

		public async Task ExecuteAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			var models = (solutionProjects ?? Enumerable.Empty<ISolutionProjectModel>()).ToArray();
			cancelledSolutions = new ConcurrentDictionary<ISolutionProjectModel, bool>();
			Cancellation = cancellation ?? new PausableCancellationTokenSource();

			await Cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
			if (!Cancellation.IsCancellationRequested)
				await ExecuteCoreAsync(models, settings, Cancellation).ConfigureAwait(false);
		}

		protected abstract Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation);

		public abstract string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType);

		public void Cancel()
		{
			Cancellation?.Cancel();
		}

		public void Cancel(ISolutionProjectModel solution)
		{
			cancelledSolutions[solution] = true;
		}

		public bool IsCancelled(ISolutionProjectModel solution)
		{
			return cancelledSolutions.ContainsKey(solution);
		}

		protected static T GetSettings<T>(IServiceSettings settings, ISolutionProjectModel model = null)
			where T : ISettingsProviderClass, new()
		{
			if (settings == null)
				return new T();
			return model == null ? settings.GetSettingsFromProvider<T>() : settings.GetSettingsFromProvider<T>(model);
		}

		protected static IList<ProjectInfo> GetProjectInfos(ISolutionProjectModel model)
		{
			return (model as SolutionProjectModel ?? SolutionParser.Parse(model.ItemPath)).Projects;
		}
	}
}
