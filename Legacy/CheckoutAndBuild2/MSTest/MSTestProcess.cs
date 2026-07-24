using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;

namespace FG.CheckoutAndBuild2.MSTest
{
	public class MSTestProcess
	{
		public string MsTestPath { get; private set; }

		/// <summary>
		/// Gets the Standard output
		/// </summary>
		public string StdOutput { get; private set; }
		
		/// <summary>
		/// Gets the Standard error
		/// </summary>
		public string StdErr { get; private set; }


		public bool CanRun
		{
			get { return File.Exists(MsTestPath); }
		}

		/// <summary>
		/// Creates a new instance of MSTest Process Wrapper
		/// </summary>
		public MSTestProcess(string vsBasePath = null)
		{
			if (string.IsNullOrEmpty(vsBasePath) || !vsBasePath.EndsWith("IDE"))
			{
				var visualStudioInfo = VisualStudioPluginHelper.GetInstalledStudios().Last();
				vsBasePath = Path.GetDirectoryName(visualStudioInfo.ExePath);
			}
			
			MsTestPath = Path.Combine(vsBasePath, "MSTest.exe");
		}

		private ScriptExecutionSettings GetScriptExecutionSettings(bool requiresAdminPrivileges)
		{
			var unitTestServiceSettings = CheckoutAndBuild2Package.GetGlobalService<SettingsService>().GetSettingsFromProvider<UnitTestServiceSettings>();
			ScriptExecutionSettings settings = unitTestServiceSettings.TrackLiveOutput ?
				ScriptExecutionSettings.Default : ScriptExecutionSettings.OneOutputStream;
			settings.RequiresAdminPrivileges = requiresAdminPrivileges;
			return settings;
		}

		public bool Run(MsTestCommand command, Action<MsTestCommand, string> onDataReceived = null, Action<MsTestCommand, string> onError = null)
		{
			Action<string> _onDataReceived = null;
			if (onDataReceived != null)
				_onDataReceived = s => onDataReceived(command, s);
			Action<string> _onError = null;
			if (onError != null)
				_onError = s => onError(command, s);
			Output.WriteLine(command.CommandString);
			var result = ScriptHelper.ExecuteScript(MsTestPath, command.CommandString, GetScriptExecutionSettings(command.RequiresAdminPrivileges), _onDataReceived, _onError);
		    return result.ProcessResult;
		}
		
		public async Task<bool> RunAsync(MsTestCommand command, Action<MsTestCommand, string> onDataReceived = null, Action<MsTestCommand, string> onError = null, 
			CancellationToken cancellationToken = default (CancellationToken))
		{

			//ProcessStartInfo startInfo = new ProcessStartInfo(MsTestPath) { Arguments = command.CommandString, Verb = "runas" };
			//var process = Process.Start(startInfo);
			//process.WaitForExit();
			//return Task.FromResult(true);
			Action<string> _onDataReceived = null;
			if (onDataReceived != null)
				_onDataReceived = s => onDataReceived(command, s);
			Action<string> _onError = null;
			if (onError != null)
				_onError = s => onError(command, s);
			var scriptExecutionSettings = GetScriptExecutionSettings(command.RequiresAdminPrivileges);			
			Output.WriteLine(command.CommandString);
			var result = await ScriptHelper.ExecuteScriptAsync(MsTestPath, command.CommandString, scriptExecutionSettings, _onDataReceived, _onError, cancellationToken);
		    return result.ProcessResult;
		}
	}
}