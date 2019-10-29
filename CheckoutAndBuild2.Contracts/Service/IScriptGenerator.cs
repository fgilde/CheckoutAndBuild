using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.Contracts.Service
{
    public interface IScriptGenerator
    {
        string GeneratePreScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType);
        string GeneratePostScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType);
    }
}