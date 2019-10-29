using System;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using LevelColors = log4net.Appender.ManagedColoredConsoleAppender.LevelColors;

namespace SolutionPacker
{
    /// <summary>
    /// 
    /// </summary>
    internal static class LoggingDescriptor
    {
        private const string patternLayoutFormat = "%date{HH:mm:ss,fff} [%level] %logger{1}\t%message %exception%newline";

        /// <summary>
        /// Initialisiert das Logging
        /// </summary>
        internal static void Initialize()
        {
            LogManager.ResetConfiguration();
            ILoggerRepository loggerRepository = LogManager.GetRepository();
            Logger logger = ((Hierarchy)loggerRepository).Root;
            logger.Level = Level.All;
            logger.AddAppender(CreateConsoleLog());
            logger.AddAppender(CreateTraceLog());
            logger.Repository.Configured = true;
        }
        
        private static TraceAppender CreateTraceLog(IFilter filter = null)
        {
            var layout = new PatternLayout(patternLayoutFormat);
            TraceAppender appender = new TraceAppender { Layout = layout };
            appender.ActivateOptions();
            if (filter != null)
                appender.AddFilter(filter);
            return appender;
        }

        private static ManagedColoredConsoleAppender CreateConsoleLog(IFilter filter = null)
        {
            var layout = new PatternLayout(patternLayoutFormat);

            var appender = new ManagedColoredConsoleAppender { Layout = layout };
            appender.AddMapping(new LevelColors { Level = Level.Notice, ForeColor = ConsoleColor.White });
            appender.AddMapping(new LevelColors { Level = Level.Info, ForeColor = ConsoleColor.White });
            appender.AddMapping(new LevelColors { Level = Level.Error, ForeColor = ConsoleColor.Red });
            appender.AddMapping(new LevelColors { Level = Level.Fatal, ForeColor = ConsoleColor.Red });
            appender.AddMapping(new LevelColors { Level = Level.Warn, ForeColor = ConsoleColor.Yellow });
            appender.ActivateOptions();
            if (filter != null)
                appender.AddFilter(filter);

            return appender;
        }
    }
}