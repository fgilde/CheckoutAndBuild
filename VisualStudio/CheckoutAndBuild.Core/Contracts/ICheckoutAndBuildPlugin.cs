using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace CheckoutAndBuild.Core.Contracts
{
    [InheritedExport]
    public interface ICheckoutAndBuildPlugin
    {
        Task Init(IServiceProvider serviceProvider, string pluginDirectory);
    }
}
