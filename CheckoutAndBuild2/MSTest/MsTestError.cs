using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace FG.CheckoutAndBuild2.MSTest
{
	public class MsTestError
	{
		public string TestPath { get; private set; }
		public string TestName { get; private set; }
		public string ErrorMessage { get; private set; }
		public int Line { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		private MsTestError(string testPath, string testName, string errorMessage, int line = 0)
		{
			TestPath = testPath;
			TestName = testName;
			ErrorMessage = errorMessage;
			Line = line;
		}

		public static IList<MsTestError> Parse(string errors)
		{
			var list = new List<MsTestError>();
			var strings = errors.Split(new[] { "Failed" }, StringSplitOptions.None).Where(s => s.Length > 5);
			foreach (var s in strings)
			{
				try
				{
					string testPath = s.Split('[')[0].Replace(" ", string.Empty).Replace("\r\n", "");
					string testName = s.Split(new[] { "[testname] =" }, StringSplitOptions.None)[1].Split('[')[0].Replace(" ", string.Empty).Replace("\r\n", "");
					string errorMessage = s.Split(new[] { "[errormessage] =" }, StringSplitOptions.None)[1];
					if (!string.IsNullOrEmpty(testName) && !string.IsNullOrEmpty(testPath) && !string.IsNullOrEmpty(errorMessage))
					{
						list.Add(new MsTestError(testPath, testName, errorMessage));
					}
				}
				catch (Exception)
				{}
			}
			return list;

		}

		public static IEnumerable<MsTestError> ParseFile(string fileName)
		{			
			return FromTestRun(TestRun.LoadFromFile(fileName));
		}

		public static IEnumerable<MsTestError> FromTestRun(TestRun runResult)
		{
			return from result in runResult.Results.Where(result => result.outcome == "Failed") 
				   let info = result.Output.ErrorInfo 
				   let fileInfo = ParseStackTrace(info.StackTrace).LastOrDefault() ?? Tuple.Create("Unknown.cs", 0) 
				   select new MsTestError(fileInfo.Item1, result.testName, info.Message + Environment.NewLine + info.StackTrace, fileInfo.Item2);
		}

		private static IList<Tuple<string, int>> ParseStackTrace(string stack)
		{
			var res = new List<Tuple<string, int>>();
			var strings = stack.Split(new[] { "in " }, StringSplitOptions.None);
			foreach (var s in strings)
			{
				try
				{
					var lineText = s.Split(':').Last();
					var fileName = s.Split(new[] { lineText }, StringSplitOptions.RemoveEmptyEntries).First();
					fileName = fileName.Substring(0, fileName.Length - 1);
					var line = lineText.Split(' ').Last();
					line = line.Substring(0, line.Length - 2);
					int lineNumber = 0;
					int.TryParse(line, out lineNumber);
					res.Add(Tuple.Create(fileName, lineNumber));
				}
				catch {}
			}
			return res;
		}
	}
}

