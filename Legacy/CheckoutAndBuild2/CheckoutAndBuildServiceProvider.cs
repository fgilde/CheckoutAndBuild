

using System;
using System.ComponentModel.Composition.Hosting;
using FG.CheckoutAndBuild2.Extensions;

namespace FG.CheckoutAndBuild2
{
    public class CheckoutAndBuildServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider package;
        private readonly CompositionContainer mefContainer;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public CheckoutAndBuildServiceProvider(CheckoutAndBuild2Package package, CompositionContainer mefContainer)
        {
            this.package = package;
            this.mefContainer = mefContainer;
        }

        /// <summary>Gets the service object of the specified type.</summary>
        /// <returns>A service object of type <paramref name="serviceType" />.-or- null if there is no service object of type <paramref name="serviceType" />.</returns>
        /// <param name="serviceType">An object that specifies the type of service object to get. </param>
        public object GetService(Type serviceType)
        {
            return package.GetService(serviceType) ?? mefContainer.GetExportedValue(serviceType);
        }
    }
}