using System;
using System.IO;
using System.Xml.Serialization;

namespace FG.CheckoutAndBuild2.MSTest
{
	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	[XmlRoot(Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010", IsNullable = false)]
	public class TestRun
	{

		public static TestRun LoadFromFile(string filename)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(TestRun));
			return serializer.Deserialize(new FileStream(filename, FileMode.Open)) as TestRun;
		}

		/// <remarks />
		public TestRunTestSettings TestSettings { get; set; }

		/// <remarks />
		public TestRunTimes Times { get; set; }

		/// <remarks />
		public TestRunResultSummary ResultSummary { get; set; }

		/// <remarks />
		[XmlArrayItem("UnitTest", IsNullable = false)]
		public TestRunUnitTest[] TestDefinitions { get; set; }

		/// <remarks />
		[XmlArrayItem("TestList", IsNullable = false)]
		public TestRunTestList[] TestLists { get; set; }

		/// <remarks />
		[XmlArrayItem("TestEntry", IsNullable = false)]
		public TestRunTestEntry[] TestEntries { get; set; }

		/// <remarks />
		[XmlArrayItem("UnitTestResult", IsNullable = false)]
		public TestRunUnitTestResult[] Results { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string id { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string runUser { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettings
	{
		/// <remarks />
		public string Description { get; set; }

		/// <remarks />
		public TestRunTestSettingsDeployment Deployment { get; set; }

		/// <remarks />
		public TestRunTestSettingsExecution Execution { get; set; }

		/// <remarks />
		public object Properties { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string id { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsDeployment
	{
		/// <remarks />
		[XmlAttribute]
		public string userDeploymentRoot { get; set; }

		/// <remarks />
		[XmlAttribute]
		public bool useDefaultDeploymentRoot { get; set; }

		/// <remarks />
		[XmlAttribute]
		public bool enabled { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string runDeploymentRoot { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecution
	{
		/// <remarks />
		public TestRunTestSettingsExecutionTestTypeSpecific TestTypeSpecific { get; set; }

		/// <remarks />
		public TestRunTestSettingsExecutionAgentRule AgentRule { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string hostProcessPlatform { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecutionTestTypeSpecific
	{
		/// <remarks />
		public TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfig UnitTestRunConfig { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfig
	{
		/// <remarks />
		public TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfigAssemblyResolution AssemblyResolution { get; set;
		}

		/// <remarks />
		[XmlAttribute]
		public string testTypeId { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfigAssemblyResolution
	{
		/// <remarks />
		public TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfigAssemblyResolutionTestDirectory TestDirectory {
			get; set; }

		/// <remarks />
		[XmlAttribute]
		public string applicationBaseDirectory { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecutionTestTypeSpecificUnitTestRunConfigAssemblyResolutionTestDirectory
	{
		/// <remarks />
		[XmlAttribute]
		public bool useLoadContext { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestSettingsExecutionAgentRule
	{
		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTimes
	{
		/// <remarks />
		[XmlAttribute]
		public DateTime creation { get; set; }

		/// <remarks />
		[XmlAttribute]
		public DateTime queuing { get; set; }

		/// <remarks />
		[XmlAttribute]
		public DateTime start { get; set; }

		/// <remarks />
		[XmlAttribute]
		public DateTime finish { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunResultSummary
	{
		/// <remarks />
		public TestRunResultSummaryCounters Counters { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string outcome { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunResultSummaryCounters
	{
		/// <remarks />
		[XmlAttribute]
		public ushort total { get; set; }

		/// <remarks />
		[XmlAttribute]
		public ushort executed { get; set; }

		/// <remarks />
		[XmlAttribute]
		public ushort passed { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte error { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte failed { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte timeout { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte aborted { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte inconclusive { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte passedButRunAborted { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte notRunnable { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte notExecuted { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte disconnected { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte warning { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte completed { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte inProgress { get; set; }

		/// <remarks />
		[XmlAttribute]
		public byte pending { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTest
	{
		/// <remarks />
		public TestRunUnitTestTestCategory TestCategory { get; set; }

		/// <remarks />
		public TestRunUnitTestDeploymentItems DeploymentItems { get; set; }

		/// <remarks />
		public TestRunUnitTestExecution Execution { get; set; }

		/// <remarks />
		public TestRunUnitTestWorkitems Workitems { get; set; }

		/// <remarks />
		public TestRunUnitTestTestMethod TestMethod { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string storage { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string id { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestTestCategory
	{
		/// <remarks />
		public TestRunUnitTestTestCategoryTestCategoryItem TestCategoryItem { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestTestCategoryTestCategoryItem
	{
		/// <remarks />
		[XmlAttribute]
		public string TestCategory { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestDeploymentItems
	{
		/// <remarks />
		public TestRunUnitTestDeploymentItemsDeploymentItem DeploymentItem { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestDeploymentItemsDeploymentItem
	{
		/// <remarks />
		[XmlAttribute]
		public string filename { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestExecution
	{
		/// <remarks />
		[XmlAttribute]
		public string id { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestWorkitems
	{
		/// <remarks />
		public ushort Workitem { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestTestMethod
	{
		/// <remarks />
		[XmlAttribute]
		public string codeBase { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string adapterTypeName { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string className { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestList
	{
		/// <remarks />
		[XmlAttribute]
		public string name { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string id { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunTestEntry
	{
		/// <remarks />
		[XmlAttribute]
		public string testId { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string executionId { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string testListId { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestResult
	{
		/// <remarks />
		public TestRunUnitTestResultOutput Output { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string executionId { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string testId { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string testName { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string computerName { get; set; }

		/// <remarks />
		[XmlAttribute(DataType = "time")]
		public DateTime duration { get; set; }

		/// <remarks />
		[XmlIgnore]
		public bool durationSpecified { get; set; }

		/// <remarks />
		[XmlAttribute]
		public DateTime startTime { get; set; }

		/// <remarks />
		[XmlAttribute]
		public DateTime endTime { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string testType { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string outcome { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string testListId { get; set; }

		/// <remarks />
		[XmlAttribute]
		public string relativeResultsDirectory { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestResultOutput
	{
		/// <remarks />
		public TestRunUnitTestResultOutputErrorInfo ErrorInfo { get; set; }
	}

	/// <remarks />
	[XmlType(AnonymousType = true, Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")]
	public class TestRunUnitTestResultOutputErrorInfo
	{
		/// <remarks />
		public string Message { get; set; }

		/// <remarks />
		public string StackTrace { get; set; }
	}
}