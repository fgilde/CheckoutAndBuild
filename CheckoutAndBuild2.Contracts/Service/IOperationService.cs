using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.Contracts.Service
{
    [InheritedExport]
    public interface IOperationService
    {
        int Order { get; }
        Guid ServiceId { get; }
        string OperationName { get; }
        Task ExecuteAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken);
        bool AllowScriptExport { get; }
        ScriptExportType[] SupportedScriptExportTypes { get; }
        string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType);
        ImageSource ContextMenuImage { get; }
        void Cancel();
        void Cancel(ISolutionProjectModel solution);
        bool IsCancelled(ISolutionProjectModel solution);
    }

    public enum ScriptExportType
    {
        Batch,
        Powershell
    }

    public static class ServiceExtensions
    {
        public static KnownOperation GetOperationType(this IOperationService service)
        {
            return GetActionType(service.ServiceId);
        }

        public static string GetPowershellFunctionName(this IOperationService service)
        {
            return service.OperationName.Replace(" ", string.Empty);
        }


        private static KnownOperation GetActionType(Guid id)
        {
            if (id == new Guid(ServiceIds.BuildServiceId))
                return KnownOperation.Build;
            if (id == new Guid(ServiceIds.CheckoutServiceId))
                return KnownOperation.Checkout;
            if (id == new Guid(ServiceIds.CleanServiceId))
                return KnownOperation.Clean;
            if (id == new Guid(ServiceIds.TestServiceId))
                return KnownOperation.Unittest;
            if (id == new Guid(ServiceIds.NugetRestoreServiceId))
                return KnownOperation.NugetRestore;
            return KnownOperation.Unknown;
        }       
    }

    public enum KnownOperation
    {
        Unknown,
        Clean,
        Checkout,
        Build,
        Unittest,
        NugetRestore
    }

}