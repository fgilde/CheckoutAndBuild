using System;
using System.Collections.Generic;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Pipeline
{
    public sealed class PipelineContext
    {
        public IReadOnlyCollection<ICustomAction> CustomActions { get; set; }

        public Func<IOperationService, ISolutionProjectModel, bool> ServiceProjectFilter { get; set; }

        public string PreBuildScript { get; set; }

        public string PostBuildScript { get; set; }

        public IServiceSettings Settings { get; set; }

        public IProgress<PipelineProgress> Progress { get; set; }
    }

    public sealed class PipelineProgress
    {
        public string OperationName { get; set; }
        public int ServiceIndex { get; set; }
        public int ServiceCount { get; set; }

        public string Error { get; set; }
    }
}
