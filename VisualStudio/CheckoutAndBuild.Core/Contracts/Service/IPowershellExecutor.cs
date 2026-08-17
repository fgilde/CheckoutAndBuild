using System.Collections.Generic;

namespace CheckoutAndBuild.Core.Contracts.Service
{
    /// <summary>
    /// Used to execute powershell scripts
    /// </summary>
    public interface IPowerShellExecutor
    {
        bool Execute(string fileNameOrContent, IDictionary<string, object> parameters = null);
    }
}
