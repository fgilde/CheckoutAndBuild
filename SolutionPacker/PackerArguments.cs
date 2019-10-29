using CommandLine;
using CommandLine.Text;

namespace SolutionPacker
{
    internal class PackerArguments
    {
        [Option('p', "path", Required = true, HelpText = "Path, where to search for *.sln files")]
        public string BasePath { get; set; }

        [Option('s', "soultionname", DefaultValue = "Build.sln", HelpText = "Name of the new solutionfile")]
        public string SLNName { get; set; }

        [Option('i', "include", DefaultValue = "*", HelpText = "Semicolon separated list of solution filenames to include")]
        public string SolutionsToInclude { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}