using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace FG.CheckoutAndBuild2.MSTest
{
	public class MsTestCommand : ICommand
	{
		public MSTestProcess MsTestProcess { get; private set; }

		public string ResultFile { get; set; }
		public ObservableCollection<UnitTestInfo> UnitTests { get; private set; }
		public string DefaultTestSettings { get; set; }
		public bool SpecifyEachTest { get; set; }
		public bool RequiresAdminPrivileges { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public MsTestCommand(params UnitTestInfo[] unitTestInfos)
		{
			SpecifyEachTest = true;			
			MsTestProcess = new MSTestProcess(Path.GetDirectoryName(Application.ExecutablePath));
			TryReadInitialSettings(unitTestInfos.FirstOrDefault());
			UnitTests = new ObservableCollection<UnitTestInfo>(unitTestInfos);
			UnitTests.CollectionChanged += (sender, args) => RaiseCanExecuteChanged();
			ResultFile = GetResultFileSuggestion();
		}

		private void TryReadInitialSettings(UnitTestInfo info)
		{			
			if (info != null && info.Project != null)
			{
				var solutionDir = info.Project.GetPropertyValue("SolutionDir");
				if (!string.IsNullOrEmpty(solutionDir) && solutionDir != "*Undefined*")
				{
					var dir = new DirectoryInfo(Path.Combine(info.Project.DirectoryPath, solutionDir));
					if (dir.Exists)
					{
						var files = dir.GetFiles("*.testsettings");
						var file = files.FirstOrDefault(fileInfo => fileInfo.Name.ToLower().Contains("local")) ?? files.FirstOrDefault();
						if (file != null && file.Exists)
							DefaultTestSettings = file.FullName;
					}
				}
			}
		}

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		public override string ToString()
		{
			return CommandString;
		}

		/// <summary>
		/// Der Command string
		/// </summary>
		public string CommandString
		{
			get
			{
				if (UnitTests == null || !UnitTests.Any())
					return string.Empty;

				string testListCommand = string.Empty;
				if (SpecifyEachTest)
					testListCommand = UnitTests.Select(info => info.TestMethodName).Aggregate("", (current, method) => current + string.Format(" /test:{0} ", method));
				string containerCommand = GetAssemblies().Aggregate("", (c, m) => c + string.Format(" /testcontainer:\"{0}\" ", m));

				string settingsOption = string.Empty;
				if (!string.IsNullOrEmpty(DefaultTestSettings))
					settingsOption = string.Format("/testsettings:\"{0}\"", DefaultTestSettings);

				var res = string.Format( "{0} /resultsfile:\"{1}\" /nologo /detail:description /detail:errormessage /detail:testname {2} {3}",containerCommand, ResultFile, settingsOption, testListCommand);
				return res;
			}
		}
		
		public bool Execute(Action<MsTestCommand, string> onDataReceived = null, Action<MsTestCommand, string> onError = null)
		{
			return MsTestProcess.Run(this, onDataReceived, onError);
		}

		public Task<bool> ExecuteAsync(Action<MsTestCommand, string> onDataReceived = null, Action<MsTestCommand, string> onError = null,
			CancellationToken cancellationToken = default (CancellationToken))
		{			
			return MsTestProcess.RunAsync(this, onDataReceived, onError, cancellationToken);
		}

		public TestRun GetTestRunResult()
		{
			if (File.Exists(ResultFile))
				return TestRun.LoadFromFile(ResultFile);
			return null;
		}

		private IEnumerable<string> GetAssemblies()
		{
			return UnitTests.Select(info => info.Assembly).Distinct();
		}

		private string GetResultFileSuggestion()
		{
			var unitTestInfo = UnitTests.FirstOrDefault();
			var resultFileMainName = unitTestInfo != null ? Path.GetFileNameWithoutExtension(unitTestInfo.Assembly) : Guid.NewGuid().ToString();
			string resultFile = Path.GetFileName(Path.GetTempFileName());
			resultFile = string.Format("{0}{1}_{2}.trx", resultFileMainName, Guid.NewGuid(), resultFile);
			resultFile = Path.Combine(Path.GetTempPath(), resultFile);
			return resultFile;
		}

		#region Member ICommand

		/// <summary>
		/// Defines the method that determines whether the command can execute in its current state.
		/// </summary>
		/// <returns>
		/// true if this command can be executed; otherwise, false.
		/// </returns>
		/// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
		public bool CanExecute(object parameter)
		{
			return UnitTests.Any() && MsTestProcess.CanRun;
		}

		/// <summary>
		/// Defines the method to be called when the command is invoked.
		/// </summary>
		/// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
		public void Execute(object parameter)
		{
			Execute();
		}

		public event EventHandler CanExecuteChanged;

		private void RaiseCanExecuteChanged()
		{
			var handler = CanExecuteChanged;
			if (handler != null) handler(this, EventArgs.Empty);
		}

		#endregion

	}
}