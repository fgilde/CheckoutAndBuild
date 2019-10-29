using System;
using System.Diagnostics;
using System.IO;

namespace CheckoutAndBuild2.CP.Plugin
{
	public static class CPFileHelper
	{
        public static bool IsSubPathOf(string path, string baseDirPath)
        {
            if (path == baseDirPath)
                return false;
            string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
                .WithEnding("\\"));

            string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
                .WithEnding("\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
        /// results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
        private static string WithEnding(this string str, string ending)
        {
            if (str == null)
                return ending;

            string result = str;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (int i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                    return tmp;
            }

            return result;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        private static string Right(this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length is less than zero");
            }

            return (length < value.Length) ? value.Substring(value.Length - length) : value;
        }


        public static void CopyFolderFast(string source, string target, bool recurive = false, bool copyOnlyIfNewer = false)
		{
		    try
		    {
		        if (!Directory.Exists(target))
		            Directory.CreateDirectory(target);
		        foreach (string file in Directory.GetFiles(source))
		        {
		            string targetFile = Path.Combine(target, Path.GetFileName(file));
		            if (copyOnlyIfNewer && File.Exists(targetFile))
		            {
		                var sourceInfo = new FileInfo(file);
		                var targetInfo = new FileInfo(targetFile);
		                if (sourceInfo.LastWriteTime > targetInfo.LastWriteTime)
		                {
		                    Trace.WriteLine($"Copy: {file} to {targetFile}");
		                    try
		                    {
		                        File.Copy(file, targetFile, true);
		                    }
		                    catch (Exception e)
		                    {
		                        Trace.WriteLine(e.Message);
		                    }
		                }
		            }
		            else
		            {
		                Trace.WriteLine($"Copy: {file} to {targetFile}");
		                try
		                {
		                    File.Copy(file, targetFile, true);
		                }
		                catch (Exception e)
		                {
		                    Trace.WriteLine(e.Message);
		                }
		            }
		        }
		        if (recurive)
		        {
		            foreach (string dir in Directory.GetDirectories(source))
		            {
		                string name = Path.GetFileName(dir);
		                if (name != null)
		                {
		                    string newTarget = Path.Combine(target, name);
		                    CopyFolderFast(dir, newTarget, true, copyOnlyIfNewer);
		                }
		            }
		        }
		    }
		    catch (Exception e)
		    {		        		        
		    }
		}

		public static void Regsvr(string dllFile)
		{
			string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
			string regsvr = Path.Combine(system, @"regsvr32.exe");
			if (File.Exists(regsvr))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(regsvr) { Arguments = "\"" + dllFile + "\" /s", Verb = "runas" };
				Process.Start(startInfo);
				//Process.Start(regsvr, "\"" + dllFile + "\" /s");
			}
			else			
				Trace.TraceError("Error: Could not Register " + Path.GetFileName(dllFile) + ". Reason: File not found(" + regsvr + ")");
			
		}

		public static void KillProcess(string processName)
		{
			foreach (var process in Process.GetProcessesByName(processName))
				process.Kill();
		}
	}
}