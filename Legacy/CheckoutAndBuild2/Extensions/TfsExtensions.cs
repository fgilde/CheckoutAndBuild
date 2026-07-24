using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.MSTest;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.WpfControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Project = Microsoft.Build.Evaluation.Project;

namespace FG.CheckoutAndBuild2.Extensions
{
	public static class TfsExtensions
	{

		public static Task<GetStatus> GetAsync(this Workspace workspace, GetRequest request, GetOptions getOptions, GetFilterCallback filterCallback, object userData, 
			CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Run(() => workspace.Get(request, getOptions, filterCallback, userData), cancellationToken).IgnoreCancellation(cancellationToken);
		}

		public static Task EndBuildAsync(this BuildManager manager, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Run(() => manager.EndBuild(), cancellationToken);
		}

		public static IEnumerable<WorkItem> AsWorkItems(this BatchedObservableCollection<WorkItemValueProvider> items)
		{
			return items.Select(provider => provider.WorkItem);
		}

		public static IEnumerable<WorkItem> AsWorkItems(this IList elements)
		{
			return elements.OfType<object>().AsWorkItems();
		}

		public static IEnumerable<WorkItem> AsWorkItems(this IEnumerable<object> elements)
		{
			var tfsContext = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
			foreach (var element in elements)
			{
				if (element is WorkItem)
					yield return element as WorkItem;
				if (element is Uri)
					yield return tfsContext.WorkItemManager.WorkItemStore.GetWorkItem(element as Uri);
				if (element is int)
					yield return tfsContext.WorkItemManager.WorkItemStore.GetWorkItem((int) element);
				if (element is WorkItemValueProvider)
					yield return ((WorkItemValueProvider) element).WorkItem;				
			}
		} 

		public static string GetPropertyValue(this ProjectRootElement element, string key)
		{
			var prop = element.Properties.FirstOrDefault(e => e.Name == key);
			if (prop != null)
				return prop.Value;
			return null;
		}

		public static Changeset FindChangesetFromTeamExplorerPage(this ITeamExplorerPage page)
		{
			PropertyInfo property = page.GetType().GetProperty("ViewModel");
			if (property != null)
			{
				var changesetDetailsPageViewModel = property.GetValue(page);
				if (changesetDetailsPageViewModel != null)
				{
					var changeSetprop = changesetDetailsPageViewModel.GetType().GetProperty("Changeset");
					if (changeSetprop != null)
					{
						var changeset = changeSetprop.GetValue(changesetDetailsPageViewModel) as Changeset;
						return changeset;
					}
				}

			}
			return null;
		}

		public static string FindMatchingLocalBasePath(this WorkingFolder[] folders)
		{
			return FindMatchingBasePath(folders, folder => folder.LocalItem);
		}

		public static string FindMatchingServerBasePath(this WorkingFolder[] folders)
		{
			return FindMatchingBasePath(folders, folder => folder.ServerItem);
		}

		public static string FindMatchingBasePath<T>(T[] items, Func<T, string> itemPath  ) 
			where T: class 
		{
			string defaultServerDir = string.Empty;
			try
			{
				foreach (T workingFolder in items)
				{
					string serverDir = itemPath(workingFolder);
					T folder1 = workingFolder;
					foreach (T f in items.Where(folder => folder != folder1))
					{
						defaultServerDir = CoreExtensions.GetUnionStringPart(serverDir, itemPath(f));
						if (!String.IsNullOrEmpty(defaultServerDir))
							break;
					}
					if (!String.IsNullOrEmpty(defaultServerDir))
						break;
				}
			}
			catch (Exception)
			{
				return string.Empty;
			}
			return defaultServerDir;
		}

		public static ProcessStartInfo GetExecutable(this Project project)
		{
			var userProject = project.FindUserProject();
		    var startupAction = userProject?.Xml.GetPropertyValue("StartAction");
		    if (!string.IsNullOrEmpty(startupAction))
		    {
		        var exe = userProject.Xml.GetPropertyValue("StartProgram");
		        var args = userProject.Xml.GetPropertyValue("StartArguments");
		        if (File.Exists(exe))
		            return new ProcessStartInfo(exe,args);
		    }

            startupAction = project.GetPropertyValue("StartAction");
            if (!string.IsNullOrEmpty(startupAction))
            {
                var exe = project.GetPropertyValue("StartProgram");
                var args = project.GetPropertyValue("StartArguments");
                if (File.Exists(exe))
                    return new ProcessStartInfo(exe, args);
            }

            var startupObject = project.GetPropertyValue("StartupObject");
            if (!string.IsNullOrEmpty(startupObject) && File.Exists(startupObject))
                return new ProcessStartInfo(startupObject, string.Empty);

            var outputFile = Check.TryCatch<FileInfo, Exception>(() => project.GetOutputFile(".exe"));
		    if (outputFile != null && outputFile.Extension == ".exe" && outputFile.Exists)
		        return new ProcessStartInfo(outputFile.FullName, string.Empty);

           
            //else
            //{
            //    string path = project.GetPropertyValue("OutputPath");
            //    if (!string.IsNullOrEmpty(path))
            //    {

            //    }
            //}
            return null;
		}

		public static Project FindUserProject(this Project project)
		{			
			string path = project.FullPath + ".user";
			if (File.Exists(path))
			{
				ProjectRootElement.Create(path);
				string s = File.ReadAllText(path);
				XmlReader reader = new XmlTextReader(new StringReader(s));
				ProjectRootElement xml = ProjectRootElement.Create(reader);
				return new Project(xml);
			}
			return null;
		}

		public static FileInfo GetFile(this ProjectItem item)
		{
			return new FileInfo(Path.Combine(item.Project.DirectoryPath, item.EvaluatedInclude));
		}

		public static IEnumerable<string> GetFiles(this WorkingFolder workingFolder, Workspace workspace, params string[] extensions)
		{
			if (workspace == null || workspace.VersionControlServer == null)
				return Enumerable.Empty<string>();
			if(extensions == null || extensions.Length == 0)
				extensions = new []{"*.*"};

			ItemSpec[] specs = extensions.Select(e => new ItemSpec(workingFolder.ServerItem + "/"+ e, RecursionType.Full)).ToArray();

			var lists = workspace.VersionControlServer.GetItems(specs, VersionSpec.Latest, DeletedState.NonDeleted, ItemType.File, false);
			return lists.SelectMany(set => set.Items).Select(i => workspace.TryGetLocalItemForServerItem(i.ServerItem));

		}

		public static bool HasReallyChange(this PendingChange pendingChange)
		{
			if (pendingChange.IsAdd || string.IsNullOrEmpty(Path.GetExtension(pendingChange.LocalItem)) || !File.Exists(pendingChange.LocalItem))
				return true;
			string tempFileName = Path.GetTempFileName();
			pendingChange.DownloadBaseFile(tempFileName);

			var diffOptions = new DiffOptions
			{
				Flags = DiffOptionFlags.IgnoreWhiteSpace 
			};

			var summary = Common.DiffSummary.DiffFiles(tempFileName,FileType.Detect(tempFileName, FileType.TextFileType), pendingChange.LocalItem, FileType.Detect(pendingChange.LocalItem, FileType.TextFileType), diffOptions);			
			var res = summary.TotalLinesAdded != 0 || summary.TotalLinesDeleted != 0 || summary.TotalLinesModified != 0;
			return res;
		}

		public static bool HasChanges(this WorkingFolder folder, Workspace workspace)
		{
			PendingChange[] pendingChanges = workspace.GetPendingChanges(folder.ServerItem, RecursionType.Full);
			return pendingChanges.Any();
		}

		public static IEnumerable<string> GetOutputDirectories(this IEnumerable<ISolutionProjectModel> solutions, bool includeTestOutputs, bool includeNotExisting)
		{
			var dirs = new List<string>();
			foreach (string dir in solutions.SelectMany(model => model.GetOutputDirectories(includeTestOutputs, includeNotExisting).Where(s => !dirs.Contains(s))))
				dirs.Add(dir);
			return dirs;
		}

		public static IEnumerable<string> GetOutputDirectories(this ISolutionProjectModel solution, bool includeTestOutputs, bool includeNotExisting)
		{
			var dirs = new List<string>();
			foreach (string dir in solution.GetSolutionProjects().Where(project => includeTestOutputs || !project.IsTestProject()).Select(project => Path.Combine(Path.GetDirectoryName(project.FullPath), project.GetOutputPath())).Where(dir => !string.IsNullOrEmpty(dir) && !dirs.Contains(dir) && (includeNotExisting || Directory.Exists(dir))))
				dirs.Add(dir);
			return dirs;
		}

		public static bool IsTestProject(this Project project)
		{
			return project.GetPropertyValue("ProjectTypeGuids").Contains(ProjectTypeGuids.TestProject);
		}

		public static bool IsDelphiProject(this Project project)
		{
			return project.Items.Any(i => i.ItemType == "DelphiCompile");
		}

		public static string GetOutputPath(this Project project)
		{
			//var res2 = project.GetPropertyValue("DCC_DcuOutput");
			return project.GetPropertyValue(project.IsDelphiProject() ? "DCC_ExeOutput" : "OutputPath");
		}

		public static FileInfo GetOutputFile(this Project project, string ext = ".dll")
		{
			var e = project.GetPropertyValue("TargetExt") ?? ext;
			if (e == "*Undefined*")
				e = ext;
			var outputPath = project.GetOutputPath();
			var assemblyName = project.GetPropertyValue("AssemblyName") + e;
			return new FileInfo(Path.Combine(Path.GetDirectoryName(project.FullPath), outputPath, assemblyName));
		}

		public static IList<UnitTestInfo> GetTestMethods(this Project project)
		{
			var list = new List<UnitTestInfo>();
			FileInfo testOutputDll = project.GetOutputFile();
			if (!testOutputDll.Exists)
				return list;
			Assembly testAssembly = Assembly.LoadFile(testOutputDll.FullName);
			//Type[] types = testAssembly.GetExportedTypes();
			Type[] types;
			try
			{
				types = testAssembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types;
			}

			foreach (Type type in types)
			{
				var type1 = type;
				var testClassAttr = Check.TryCatch<Attribute, Exception>(() => type1.GetCustomAttribute(typeof(TestClassAttribute)));
				if (testClassAttr != null)
				{					
					var members = Check.TryCatch<MethodInfo[], Exception>(() => type1.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod));
					foreach (MethodInfo member in members)
					{
						var member1 = member;
						Attribute testMethodAttr = Check.TryCatch<Attribute, Exception>(() => member1.GetCustomAttribute(typeof (TestMethodAttribute)));
						Attribute ignoreAttr = Check.TryCatch<Attribute, Exception>(() => member1.GetCustomAttribute(typeof(IgnoreAttribute)));
						if (testMethodAttr != null && ignoreAttr == null)
						{
							var unitTestInfo = new UnitTestInfo(testOutputDll.FullName, member, project);
							foreach (var t in member.CustomAttributes)
							{
								if (t.AttributeType.Name == "OwnerAttribute")
									unitTestInfo.Owner = t.ConstructorArguments[0].Value.ToString();
								else if (t.AttributeType.Name == "PriorityAttribute")
									unitTestInfo.Priority = Int32.Parse(t.ConstructorArguments[0].Value.ToString());
							}
							list.Add(unitTestInfo);
						}
					}
				}
			}

			return list;
		}

		/// <summary>
		/// Gibt für das IEnumerable eine DataTable zurück, die z.B als DataSource benutzt werden kann
		/// </summary>
		public static DataTable AsDataTable<T>(this IEnumerable<T> source)
		{
			var dataTableResult = new DataTable();
			PropertyInfo[] propertyInfos = typeof(T).GetProperties();
			foreach (var propertyInfo in propertyInfos)
			{
				dataTableResult.Columns.Add(new DataColumn(propertyInfo.Name, propertyInfo.PropertyType));
			}
			foreach (var entry in source)
			{
				DataRow newRow = dataTableResult.NewRow();
				foreach (var propertyInfo in propertyInfos)
				{
					var value = propertyInfo.GetValue(entry, new object[0]);
					newRow[propertyInfo.Name] = value;
				}
				dataTableResult.Rows.Add(newRow);
			}
			return dataTableResult;
		}

		public static string GetCommiterUniqueName(this Changeset changeset)
		{
			return changeset.Committer; // z.B CP\floriang
		}

		// Wichtig: Die rückgabe hier muss die gleiche sein wie bei GetCommiterUniqueName(Changset) damit ein Changset mapping für eine TeamFoundationIdentity funktioniert
		public static string GetUniqueName(this TeamFoundationIdentity identity)
		{
			PropertyInfo propertyInfo = identity.GetType().GetProperty("UniqueName");
			if (propertyInfo != null)
				return ExposedObject.From(identity).UniqueName;

			try
			{
				var identifier = new SecurityIdentifier(identity.Descriptor.Identifier);
				var ntAccount = (NTAccount)(identifier.Translate(typeof(NTAccount)));
				return ntAccount.ToString();
			}
			catch (Exception)
			{
				return identity.DisplayName;
			}
		}

		public static IEnumerable<ListViewItem> ToEnumerable(this ListView.ListViewItemCollection viewItemCollection)
		{
			return viewItemCollection.Cast<ListViewItem>();
		}

		/// <summary>
		/// Fungiert genauso wie "Where", ausser dass ein delegate für weitere children 
		/// übergeben werden muss, und diese dann rekursiv mit einbezogen werden 
		/// </summary>
		public static IEnumerable<TSource> Recursive<TSource>(this IEnumerable<TSource> children, Func<TSource, IEnumerable<TSource>> childDelegate)
		{
			return Recursive(children, childDelegate, source => true);
		}

		/// <summary>
		/// Fungiert genauso wie "Where", ausser dass ein delegate für weitere children 
		/// übergeben werden muss, und diese dann rekursiv mit einbezogen werden 
		/// </summary>
		public static IEnumerable<TSource> Recursive<TSource>(this IEnumerable<TSource> children, Func<TSource, IEnumerable<TSource>> childDelegate, Func<TSource, bool> predicate)
		{
			foreach (var source in children)
			{
				var grandchildren = childDelegate(source);
				foreach (var grandchild in Recursive(grandchildren, childDelegate, predicate))
					yield return grandchild;
				if (predicate(source))
					yield return source;
			}
		}

		public static string GetUnionStringPart(string s1, string s2)
		{
			string res = "";
			string longStr;
			string smallStr;
			if (s1.Length > s2.Length)
			{
				longStr = s1;
				smallStr = s2;
			}
			else
			{
				longStr = s2;
				smallStr = s1;
			}
			int i = 0;
			foreach (char c in smallStr)
			{
				if (c == longStr[i])
					res += c;
				else
					break;
				i++;
			}
			return res;
		}

		//public static string FindCalnderWeekIterationName(this DateTime date)
		//{
		//	string name = string.Format("{1}KW {0}", date.CalendarWeek().ToString(CultureInfo.InvariantCulture).Zerorize(), GetYearName(date));
		//	if (CheckoutAndBuildLogic.Instance.IsConnected)
		//	{
		//		var iterations = CheckoutAndBuildLogic.Instance.GetIterations().Select(node => node.Name).ToList();
		//		if (!iterations.Contains(name))
		//		{
		//			int week = date.CalendarWeek();
		//			string result = iterations.Where(s => s.StartsWith(GetYearName(date))).FirstOrDefault(iteration =>
		//			{
		//				int iterationCalendarWeek = iteration.Substring(iteration.Length - 2).ToInt();
		//				if (iterationCalendarWeek > -1)
		//					return iterationCalendarWeek > week;
		//				return false;
		//			});
		//			if (string.IsNullOrEmpty(result))
		//				result = iterations.LastOrDefault();
		//			return result ?? string.Empty;
		//		}
		//	}
		//	return name;

		//}

		private static string GetYearName(DateTime time)
		{
			return time.Year.ToString(CultureInfo.InvariantCulture).Substring(2);
		}

		public static int ToInt(this string s)
		{
			int i;
			if (int.TryParse(s, out i))
				return i;
			return -1;
		}

		public static string Zerorize(this string s)
		{
			int i;
			if (int.TryParse(s, out i))
			{
				if (i < 10)
					return string.Format("0{0}", i);
				return i.ToString(CultureInfo.InvariantCulture);
			}
			return s;
		}

		public static int CalendarWeek(this DateTime date)
		{
			//CultureInfo CUI = CultureInfo.CurrentCulture;
			return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
		}

		public static string ToTimeString(this TimeSpan span)
		{
			//return span.ToString();
			string formatted = string.Format("{0}{1}{2}{3}",
				span.Duration().Days > 0 ? string.Format("{0:0} d ", span.Days) : string.Empty,
				span.Duration().Hours > 0 ? string.Format("{0:0} h ", span.Hours) : string.Empty,
				span.Duration().Minutes > 0 ? string.Format("{0:0} min ", span.Minutes) : string.Empty,
				span.Duration().Milliseconds > 0 ? string.Format("{0:0},{1:1} s", span.Seconds, span.Milliseconds) : string.Empty);

			if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

			if (string.IsNullOrEmpty(formatted)) formatted = span.Milliseconds + " ms";

			return formatted;
		}


		/// <summary>
		/// WriteDictionary to StringBuilder
		/// </summary>
		public static void WriteDictionary(Dictionary<string, object> dictionary, StringBuilder sb)
		{
			foreach (var keyValuePair in dictionary)
				sb.AppendLine(String.Format(" {0}={1}", keyValuePair.Key, keyValuePair.Value));
		}

		/// <summary>
		/// WriteDictionary to StringBuilder
		/// </summary>
		public static void AppendDictionary(this StringBuilder sb, Dictionary<string, object> dictionary)
		{
			WriteDictionary(dictionary, sb);
		}

		//public static Version GetBranchVersion(this WorkingFolder workingFolder)
		//{
		//	Version result = null;
		//	var name = workingFolder.BranchName();
		//	if (name.Contains("Release_"))
		//		name = name.Replace("Release_", "");
		//	if (name != "Head" && Version.TryParse(name, out result))
		//		return result;
		//	string lastBranch = CheckoutAndBuildLogic.Instance.GetOldCPReleaseBranchNames().OrderBy(s => s).LastOrDefault();
		//	if (!string.IsNullOrEmpty(lastBranch))
		//	{
		//		string releasename = lastBranch.Split('/').LastOrDefault(s => !s.ToLower().EndsWith("solution"));
		//		if (!string.IsNullOrEmpty(releasename))
		//		{
		//			if (releasename.Contains("Release"))
		//				releasename = releasename.Replace("Release_", "");
		//			if (Version.TryParse(releasename, out result))
		//			{
		//				return new Version(result.Major, result.Minor + 1);
		//			}
		//		}
		//	}
		//	return null;
		//}

		public static string BranchName(this WorkingFolder workingFolder)
		{
			if (workingFolder.ServerItem.ToLower().Contains("/features/"))
			{ // FB Branch
				string name = workingFolder.ServerItem.Split('/')[3];
				return name;
			}
			else
			{
				string name = workingFolder.ServerItem.Split('/').LastOrDefault(s => !s.ToLower().EndsWith("solution"));
				if (!string.IsNullOrEmpty(name))
				{
					if (name.ToUpper() == "MSDTC")
						return name;
					if (name.Contains("Release"))
						return name;
					return "Head";
				}
			}
			return "Head";
		}



	}
}