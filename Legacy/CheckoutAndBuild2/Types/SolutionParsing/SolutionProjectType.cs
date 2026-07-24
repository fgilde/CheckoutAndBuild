namespace Microsoft.Build.Construction
{
    public enum SolutionProjectType
    {
        Unknown,
        KnownToBeMSBuildFormat,
        SolutionFolder,
        WebProject,
        WebDeploymentProject,
        EtpSubProject,
    }
}