using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Build;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace FG.CheckoutAndBuild2.Extensions
{
	public static class ServiceProviderExtensions
	{
		private static ITrackSelection trackSelection;
		private static readonly Dictionary<Type, Type> vsServiceMapping = new Dictionary<Type, Type>
		{
			{typeof(DTE), typeof(SDTE)},
			{typeof(DTE2), typeof(SDTE)},			
			{typeof(IVsSccToolsOptions), typeof(SVsSccToolsOptions)},
			{typeof(IVsSccManager2), typeof(SVsSccManager)},			
			{typeof(IVsExtensionManager), typeof(SVsExtensionManager)},
			{typeof(IVsExtensionRepository), typeof(SVsExtensionRepository)},
			{typeof(MenuCommandService), typeof(IMenuCommandService)},
			{typeof(VsTeamFoundationBuild), typeof(IVsTeamFoundationBuild)},
		};

        public static object GetExportedValue(this ExportProvider container, Type type)
        {
            // get a reference to the GetExportedValue<T> method
            MethodInfo methodInfo = container.GetType().GetMethods().First(d => d.Name == "GetExportedValueOrDefault"&& d.GetParameters().Length == 0);


            // add the generic types to the method
            methodInfo = methodInfo.MakeGenericMethod(type);

            // invoke GetExportedValue<type>()
            return methodInfo.Invoke(container, null);
        }

        public static T Get<T>(this IServiceProvider serviceProvider)
		{
			if (typeof (T) == typeof (ITrackSelection))
				return (T)(trackSelection ?? (trackSelection = VisualStudioDTE.GetITrackSelection(VisualStudioIds.TeamExplorerToolWindowId.ToGuid())));
			if (serviceProvider != null)
			{
				if(vsServiceMapping.ContainsKey(typeof(T)))
					return (T)serviceProvider.GetService(vsServiceMapping[typeof(T)]);
				if (typeof(T) == typeof(VersionControlExt))
					return ((T)Get<DTE2>(serviceProvider).GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt"));
				return (T)serviceProvider.GetService(typeof(T));
			}
			return default(T);
		}
	
	}
}