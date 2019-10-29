using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;

namespace CheckoutAndBuild2.CP.Plugin
{
    public class CPPlugin : ICheckoutAndBuildPlugin
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static T Get<T>()
        {
            return (T) ServiceProvider?.GetService(typeof(T));
        }

        public Task Init(IServiceProvider serviceProvider, string pluginDirectory)
        {
            ServiceProvider = serviceProvider;
            return Task.CompletedTask;
        }
    }
}