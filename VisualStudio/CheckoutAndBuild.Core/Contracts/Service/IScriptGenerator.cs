using System.Collections.Generic;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Contracts.Service
{
    public interface IScriptGenerator
    {
        string GeneratePreScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType);
        string GeneratePostScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType);
    }
}
