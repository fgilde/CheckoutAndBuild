using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using CheckoutAndBuild2.Contracts.Service;
using FG.CheckoutAndBuild2.Extensions;

namespace FG.CheckoutAndBuild2.Common
{
	public static class ScriptHelper
	{

		#region Internal helpers

		[DllImport("kernel32.dll")]
		private static extern bool CreateSymbolicLink(
			string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

		private enum SymbolicLink
		{
			File = 0,
			Directory = 1
		}

		internal static bool IsAdmin
		{
			get
			{
				WindowsIdentity id = WindowsIdentity.GetCurrent();
				if (id != null)
				{
					var p = new WindowsPrincipal(id);
					return p.IsInRole(WindowsBuiltInRole.Administrator);
				}
				return true;
			}
		}

		#endregion

		#region RegSvr u Regasm

		public static void Regsvr(string dllFile)
		{
			string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
			string regsvr = Path.Combine(system, @"regsvr32.exe");
			if (File.Exists(regsvr))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(regsvr) {Arguments = "\"" + dllFile + "\" /s", Verb = "runas"};
				Process.Start(startInfo);
				//Process.Start(regsvr, "\"" + dllFile + "\" /s");
			}
			else
			{
				Trace.TraceError("Error: Could not Register " + Path.GetFileName(dllFile) + ". Reason: File not found(" + regsvr +
								")");
			}
		}

		public static void Regasm(string dllFile)
		{
			string args = "\"" + dllFile + "\"" + " /codebase";
			string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			string regasm = Path.Combine(windows, @"Microsoft.NET\Framework\v4.0.30319\RegAsm.exe");
			if (File.Exists(regasm))
			{
				ExecuteScript(regasm, args, new ScriptExecutionSettings(true, true, false));
			}
			else
			{
				Trace.TraceError("Error: Could not Register " + Path.GetFileName(dllFile) + ". Reason: File not found(" + regasm +
								")");
			}
			string regasm64 = Path.Combine(windows, @"Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe");
			if (File.Exists(regasm64))
			{
				ExecuteScript(regasm64, args, new ScriptExecutionSettings(true, true, false));
			}
			else
			{
				Trace.TraceError("Error: Could not Register x64 " + Path.GetFileName(dllFile) + ". Reason: File not found(" +
								regasm64 + ")");
			}
		}

		#endregion

		public static Task<Collection<PSObject>> ExecutePowershellScriptAsync(string fileNameOrContent,
			IDictionary<string, object> parameters = null,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.Run(() => ExecutePowershellScript(fileNameOrContent, parameters), cancellationToken);
		}

		public static Collection<PSObject> ExecutePowershellScript(string fileNameOrContent, IDictionary<string, object> parameters = null,
			Action<PSObject> onDataReceived = null, Action<ErrorRecord> onError = null)
		{
            if(parameters == null)
                parameters = new Dictionary<string, object>();
            parameters.Add("scriptPath", File.Exists(fileNameOrContent) ? fileNameOrContent : "");
            string content = File.Exists(fileNameOrContent) ? File.ReadAllText(fileNameOrContent) : fileNameOrContent;
			if (onDataReceived == null) onDataReceived = o => Output.WriteLine(o.ToString());
			if (onError == null) onError = o => Output.Exception(o.Exception);
			using (PowerShell psi = PowerShell.Create())
			{                                
				psi.AddScript(content);				
				if (parameters != null && parameters.Count > 0)
				{					
					psi.AddParameters((IDictionary)parameters);
					//parameters.Apply(pair => psi.Commands.AddParameter(pair.Key, pair.Value));
				}
				psi.Commands.AddCommand("Out-String");				
                Collection<PSObject> res = psi.Invoke();
				foreach (var psObject in res.Where(psObject => psObject != null))
					onDataReceived(psObject);

				foreach (ErrorRecord errorRecord in psi.Streams.Error.ReadAll())
					onError(errorRecord);

				return res;
			} 
		}


        public static Task<ScriptExecutingResult> ExecuteScriptAsync(string fileName, string arguments,
			ScriptExecutionSettings settings,
			Action<string> onDataReceived = null, Action<string> onError = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {            
			return Task.Run(() => ExecuteScript(fileName, arguments, settings, onDataReceived, onError, cancellationToken), cancellationToken);
		}

	    public static bool IsPowerShell(string filename)
	    {
            string [] extensions = { ".ps1", "psm1", "psd1", "ps1xml" };
	        var extension = Path.GetExtension(filename);
	        return !string.IsNullOrEmpty(extension) && extensions.Contains(extension.ToLower());
	    }



	    public static ScriptExecutingResult ExecuteScript(string fileName, string arguments,
			ScriptExecutionSettings settings,            
			Action<string> onDataReceived = null, Action<string> onError = null, 
			CancellationToken cancellationToken = default(CancellationToken))
	    {
	        Process process = null;
			if (onDataReceived == null) onDataReceived = Output.WriteLine;
			if (onError == null) onError = Output.WriteLine;

			if (cancellationToken.IsCancellationRequested)
				return ScriptExecutingResult.False();

			#region Check for Powershell

			if (IsPowerShell(fileName))
			{
				try
				{
					var r = ExecutePowershellScript(fileName, null, o => onDataReceived(o.ToString()), record => onError(record.Exception.Message));
					return ScriptExecutingResult.FromResult(r != null && r.Any());
				}
				catch (Exception)
				{
					return ScriptExecutingResult.False();
				}
			}

            #endregion

	        if (settings.ExecuteWithCmd)
	        {
	            arguments = $"/c {fileName} " + arguments;
	            fileName = "cmd";
	        }

			bool requiresAdminPrivileges = settings.RequiresAdminPrivileges && !IsAdmin; 

			bool result = false;
			try
			{
				var workingDirectory = settings.WorkingDirectory ?? Path.GetDirectoryName(fileName);
				//workingDirectory = @"C:\Dev\HEAD\Tests\CP.Air";

				var processInfo = new ProcessStartInfo(fileName) {Verb = settings.RequiresAdminPrivileges ? "runas" : string.Empty };
				if (!string.IsNullOrEmpty(workingDirectory))
					processInfo.WorkingDirectory = workingDirectory;
				if (settings.IsHidden && !requiresAdminPrivileges)
				{
					processInfo.UseShellExecute = false;
					processInfo.RedirectStandardError = true;
					processInfo.RedirectStandardInput = true;
					processInfo.RedirectStandardOutput = true;
					processInfo.CreateNoWindow = true;
				}
				if (!string.IsNullOrEmpty(arguments))
					processInfo.Arguments = arguments;
				var p = new Process { StartInfo = processInfo };
			    process = p;
                cancellationToken.Register(() =>
				{
					Check.TryCatch<Exception>(() =>
					{
						if (!p.HasExited)
							p.Kill();
					});
				});
				if (settings.IsHidden)
				{
					p.OutputDataReceived += (sender, args) =>
					{
						if (!cancellationToken.IsCancellationRequested && onDataReceived != null)
							onDataReceived(args.Data);
					};
					p.ErrorDataReceived += (sender, args) =>
					{
						if (!cancellationToken.IsCancellationRequested && onError != null)
							onError(args.Data);
					};
					//p.EnableRaisingEvents = true;
                    processInfo.StandardOutputEncoding = Encoding.UTF8;
				}

				p.EnableRaisingEvents = true;
		
				if (cancellationToken.IsCancellationRequested)
					return ScriptExecutingResult.False(p);

				result = p.Start();
				p.Exited += (sender, args) => result = result && p.ExitCode == 0;
				if (settings.TrackLiveOutput && p.StartInfo.RedirectStandardOutput)
				{
					p.BeginOutputReadLine();
					if (settings.WaitForProcessExit)
						p.WaitForExit();
				}
				else
				{
					if (settings.IsHidden && p.StartInfo.RedirectStandardOutput)
					{
						StreamWriter sw = p.StandardInput;
						StreamReader sr = p.StandardOutput;
						StreamReader err = p.StandardError;
						sw.AutoFlush = true;
						sw.WriteLine(" ");
						sw.Close();

						string output = sr.ReadToEnd();
						string error = err.ReadToEnd();
						if (!cancellationToken.IsCancellationRequested)
						{
							onDataReceived(output);
							onError(error);
						}
					}
					if (settings.WaitForProcessExit)
						p.WaitForExit();
				}

			}
			catch (Exception e)
			{
				Output.WriteLine(e.Message);
			}
			return ScriptExecutingResult.FromResult(result, process);
		}

		public static void CreateSymbolicLink(SymbolLinkInfo info)
		{
			CreateSymbolicLink(info.LinkName, info.Target);
		}

		public static void CreateSymbolicLink(string linkName, string target)
		{
			SymbolicLink linkType = string.IsNullOrEmpty(Path.GetExtension(linkName)) ? SymbolicLink.Directory : SymbolicLink.File;
			if (linkType == SymbolicLink.Directory)
			{
				var info = new DirectoryInfo(linkName);
				if (info.Parent != null && !Directory.Exists(info.Parent.FullName))
					Directory.CreateDirectory(info.Parent.FullName);
			}
			else if (linkType == SymbolicLink.File)
			{
				var info = new FileInfo(linkName);
				if (info.Directory != null && !info.Directory.Exists)
					Directory.CreateDirectory(info.Directory.FullName);
			}
			try
			{
				CreateSymbolicLink(linkName, target, linkType);
			}
			catch { }
		}

		public static void RemoveSymbolicLink(string linkName)
		{
			if (string.IsNullOrEmpty(Path.GetExtension(linkName)) && File.Exists(linkName))
			{
				File.Delete(linkName);
			}
			else if (Directory.Exists(linkName) && !Directory.GetFiles(linkName).Any())
			{
				try
				{
					Directory.Delete(linkName);
				}
				catch (Exception)
				{ }
			}
		}

		public static string PrepareScriptVars(string script, bool removeDuplicateLines, ScriptExportType scriptType)
		{
		    return scriptType == ScriptExportType.Batch ? PrepareBatchScriptVars(script, removeDuplicateLines) : PreparePowershellScriptVars(script, removeDuplicateLines);
		}

	    private static string PreparePowershellScriptVars(string script, bool removeDuplicateLines)
	    {
            var result = script;
            if (removeDuplicateLines)
                return RemoveDuplicateLines(result);
            return result;
	    }

	    private static string RemoveDuplicateLines(string result)
	    {
	        var previousLines = new HashSet<string>();
            return new StringBuilder()
                .AppendLines(result.GetLines().Where(line => String.IsNullOrWhiteSpace(line) || Allowed(line) || previousLines.Add(line)))
                .ToString();	        
	    }

	    private static bool Allowed(string line)
	    {
	        return line.Length < 2;
	    }

	    private static string PrepareBatchScriptVars(string script, bool removeDuplicateLines)
	    {
	        var pre = new StringBuilder();
	        int index = 0;
	        var dict = new Dictionary<string, string>();
	        var dict2 = new Dictionary<string, string>();
	        MatchCollection matchList = Regex.Matches(script, "([\"'])(?:(?=(\\\\?))\\2.)*?\\1");
	        var list = matchList.Cast<Match>().Where(match => !string.IsNullOrEmpty(match.Value)).ToList();
	        foreach (Match match in list)
	        {
	            string value = match.Value.Replace("\"", "");
	            string key = FindKey(value);
	            if (!dict.ContainsValue(value))
	            {
	                if (dict.ContainsKey(key))
	                    key += "_" + (++index);
	                dict.Add(key, value);
	            }
	            else
	            {
	                if (!dict2.ContainsKey(key))
	                    dict2.Add(key, value);
	                if (!dict.ContainsKey(key))
	                    dict.Remove(key);
	            }
	        }
	        foreach (var pair in dict2)
	        {
	            pre.AppendLine(string.Format("SET {0}={1}", pair.Key, pair.Value));
	            script = script.Replace(pair.Value, string.Format("%{0}%", pair.Key));
	        }
	        var result = pre.AppendLine() + script;
            if (removeDuplicateLines)
                return RemoveDuplicateLines(result);
            return result;
	    }

	    private static string FindKey(string value)
		{
			string res = value.Replace("\"", "");
			if (File.Exists(res))
				res = string.Format("{0}", Path.GetFileName(res));
			else if (res.Contains(" "))
			{
				var indexOf = res.IndexOf(" ");
				res = res.Substring(0, indexOf);
			}
			return res.Replace(".", "_").Replace(" ", "").Replace(":", "_").Replace("\\", "").ToUpper();
		}



	}

    public class ScriptExecutingResult
    {
        private ScriptExecutingResult()
        {}

        public bool ProcessResult { get; private set; }
        public Process Process { get; private set; }

        public static ScriptExecutingResult FromResult(bool result, Process process = null)
        {
            return new ScriptExecutingResult { Process = process, ProcessResult = result };
        }

        public static ScriptExecutingResult False(Process process = null)
        {
            return FromResult(false, process);
        }

        public static ScriptExecutingResult True(Process process = null)
        {
            return FromResult(true, process);
        }

        public override string ToString()
        {
            return ProcessResult.ToString();
        }
    }

	public class SymbolLinkInfo
	{
		public string LinkName { get; private set; }
		public string Target { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public SymbolLinkInfo(string linkName, string target)
		{
			LinkName = linkName;
			Target = target;
		}
	}
}