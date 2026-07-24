using System;
using Microsoft.Build.Construction;

internal class ProjectConfigurationInSolution : ConfigurationInSolution
{
    private bool includeInBuild;

    internal bool IncludeInBuild
    {
        get
        {
            return this.includeInBuild;
        }
    }

    internal ProjectConfigurationInSolution(string configurationName, string platformName, bool includeInBuild)
      : base(configurationName, ProjectConfigurationInSolution.RemoveSpaceFromAnyCpuPlatform(platformName))
    {
        this.includeInBuild = includeInBuild;
    }

    private static string RemoveSpaceFromAnyCpuPlatform(string platformName)
    {
        if (string.Compare(platformName, "Any CPU", StringComparison.OrdinalIgnoreCase) == 0)
            return "AnyCPU";
        return platformName;
    }
}