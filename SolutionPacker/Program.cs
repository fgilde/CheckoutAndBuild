using System;
using System.IO;
using CommandLine;
using log4net;

namespace SolutionPacker
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            LoggingDescriptor.Initialize();

            var arguments = new PackerArguments();
            if (Parser.Default.ParseArguments(args, arguments))
            {
                Packer.CobbleTogether(arguments);
                log.Info($"New solution {Path.Combine(arguments.BasePath, arguments.SLNName)} successfully created");
            }
        }
    }
}
