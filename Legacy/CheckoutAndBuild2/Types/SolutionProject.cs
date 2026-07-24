//using System;
//using System.Diagnostics;
//using System.Reflection;

//namespace FG.CheckoutAndBuild2.Types
//{
//	[DebuggerDisplay("{ProjectName}, {RelativePath}, {ProjectGuid}")]
//	public class SolutionProject
//	{
//		static readonly Type projectInSolution;
//		static readonly PropertyInfo projectInSolutionProjectName;
//		static readonly PropertyInfo projectInSolutionRelativePath;
//		static readonly PropertyInfo projectInSolutionProjectGuid;
//		static readonly PropertyInfo projectInSolutionProjectType;

//		static SolutionProject()
//		{
//			projectInSolution = Type.GetType("Microsoft.Build.Construction.ProjectInSolution, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, false);
//			if (projectInSolution != null)
//			{
//				projectInSolutionProjectName = projectInSolution.GetProperty("ProjectName", BindingFlags.NonPublic | BindingFlags.Instance);
//				projectInSolutionRelativePath = projectInSolution.GetProperty("RelativePath", BindingFlags.NonPublic | BindingFlags.Instance);
//				projectInSolutionProjectGuid = projectInSolution.GetProperty("ProjectGuid", BindingFlags.NonPublic | BindingFlags.Instance);
//				projectInSolutionProjectType = projectInSolution.GetProperty("ProjectType", BindingFlags.NonPublic | BindingFlags.Instance);
//			}
//		}

//		public string ProjectName { get; private set; }
//		public string RelativePath { get; private set; }
//		public string ProjectGuid { get; private set; }
//		public SolutionProjectType ProjectType { get; private set; }

//		public SolutionProject(object solutionProject)
//		{
//			ProjectName = projectInSolutionProjectName.GetValue(solutionProject, null) as string;
//			RelativePath = projectInSolutionRelativePath.GetValue(solutionProject, null) as string;
//			ProjectGuid = projectInSolutionProjectGuid.GetValue(solutionProject, null) as string;
//			ProjectType = (SolutionProjectType)projectInSolutionProjectType.GetValue(solutionProject, null);
//		}
//	}

//	public enum SolutionProjectType
//	{
//		Unknown,
//		KnownToBeMSBuildFormat,
//		SolutionFolder,
//		WebProject,
//		WebDeploymentProject,
//		EtpSubProject,
//	}
//}
