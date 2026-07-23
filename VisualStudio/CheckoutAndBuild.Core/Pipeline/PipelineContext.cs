using System;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Pipeline
{
    public sealed class PipelineContext
    {
        /// <summary>Optional path to a .bat/.cmd/.ps1 run before any service; non-zero exit aborts the pipeline.</summary>
        public string PreBuildScript { get; set; }

        /// <summary>Optional path to a .bat/.cmd/.ps1 run right after the build service; failures are reported, not thrown.</summary>
        public string PostBuildScript { get; set; }

        public IServiceSettings Settings { get; set; }

        public IProgress<PipelineProgress> Progress { get; set; }
    }

    public sealed class PipelineProgress
    {
        public string OperationName { get; set; }
        public int ServiceIndex { get; set; }
        public int ServiceCount { get; set; }

        /// <summary>Non-null when this report signals a non-fatal error (failing service, failing post-build script).</summary>
        public string Error { get; set; }
    }
}
