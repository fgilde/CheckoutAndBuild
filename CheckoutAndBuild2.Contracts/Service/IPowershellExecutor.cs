using System.Collections.Generic;

namespace CheckoutAndBuild2.Contracts.Service
{
    /// <summary>
    /// Used to execute powershell scripts
    /// </summary>
    public interface IPowerShellExecutor
    {
        /// <summary>
        /// Executes a powershell script
        /// </summary>
        /// <param name="fileNameOrContent">Eiter the path to the script or the script itself</param>
        /// <param name="parameters">parameters, can be null</param>
        /// <returns></returns>
        bool Execute(string fileNameOrContent, IDictionary<string, object> parameters = null);
    }
}
