using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using CheckoutAndBuild2.Contracts.Service;
using FG.CheckoutAndBuild2.Common;

namespace FG.CheckoutAndBuild2.Services
{
    [Export(typeof(IPowerShellExecutor))]
    public class PowerShellExecutor : IPowerShellExecutor
    {
        public bool Execute(string fileNameOrContent, IDictionary<string, object> parameters = null)
        {
            var result = ScriptHelper.ExecutePowershellScript(fileNameOrContent, parameters);
            return result != null && result.Any();
        }
    }
}
