using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.Build.Evaluation;

namespace FG.CheckoutAndBuild2.MSTest
{
	public class UnitTestInfo
	{
		public string Assembly { get; private set; }
		public string TestMethodName { get; private set; }
		public MemberInfo TestMethodInfo { get; private set; }
		public Project Project { get; private set; }
		public ProjectItem ProjectFileItem { get; private set; }

		public int Line { get; private set; }
		public int Column { get; private set; }
		public int Priority { get; set; }
		public string Owner { get; set; }

		//public MsTestCommand MsTestCommand { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public UnitTestInfo(string assembly, MemberInfo testMethod, Project project)
		{
			Line = Column = 0;
			Priority = -1;
			Owner = string.Empty;
			Assembly = assembly;
			TestMethodInfo = testMethod;
			TestMethodName = testMethod.Name;
			Project = project;
			//MsTestCommand = new MsTestCommand(this);
			Task.Run(() => FindProjectItem(testMethod.DeclaringType, project));
		}

		private void FindProjectItem(Type declaringType, Project project)
		{
			// TODO: Besser suchen (prüfen ob der Type in dem Item enthalten ist oder so)
			ProjectFileItem = project.GetItems("Compile").FirstOrDefault(item => item.EvaluatedInclude.Contains(declaringType.Name));
			if (ProjectFileItem != null && (ProjectFileItem.GetFile().Exists))
				UpdateLineColumnInfo();
		}

		private void UpdateLineColumnInfo()
		{
			var lines = File.ReadAllLines(ProjectFileItem.GetFile().FullName);
			var line = lines.FirstOrDefault(s => s.ToLower().Contains(string.Format("public void {0}", TestMethodName).ToLower()));
			if (!string.IsNullOrEmpty(line))
			{
				Line = lines.IndexOf(line) + 1;
				if (Line > 1)
					Column = line.IndexOf(TestMethodName, StringComparison.InvariantCultureIgnoreCase);
			}
		}
	}
}