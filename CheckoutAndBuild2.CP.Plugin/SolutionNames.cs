using System.Linq;

namespace CheckoutAndBuild2.CP.Plugin
{
	public static class SolutionNames
	{
		public static string PlannerEmbedded => "CPlannerEmbedded";
	    public static string ServerEmbedded => "CPServerEmbedded";
	    public static string DataExchange => "CP.DataExchange.sln";
	    public static string Suite => "CP.Suite.sln";
	    public static string WebModule => "CP.WebModule.sln";
	    public static string Database => "CP.Database.sln";
	    public static string ControlCenter => "CP.CONTROLCENTER.sln";
	    public static string CorporatePlanner => "CP.Planner.sln";
	    public static string RiskManager => "CP.RiskManager.sln";
	    public static string Air => "CP.Air.sln";
	    public static string StrategicPlanner => "CP.StrategicPlanner.sln";
	    public static string Contracts => "CP.Contracts.sln";
	    public static string Common => "CP.Common.sln";
	    public static string CommonBO => "CP.CommonBO.sln";
	    public static string Finance => "CP.Finance.sln";
	    public static string Integration => "CP.Integration.sln";
	    public static string Cons => "CP-CONS.Build.sln";
	    public static string ConsUpdater => "CP.CONS.Updater.sln";
	    public static string ApplicationServer => "CP.ApplicationServer.sln";
	    public static string CPServer => "CP.Server.sln";
	    public static string Sales => "CP.Sales.sln";
	    public static string Dashboard => "CP.Dashboard.sln";
	    public static string WebComponents => "CP WebComponents.sln";
	    public static string Workflow => "CP.ProcessMonitor.sln";
	    public static string WebBSC => "CP.Web.Bsc.sln";
	    public static string eBilanz => "CP.eBilanz.sln";

	    public static bool IsWebApplicationTargetRequired(string solutionName)
		{
			return new[] { Air, WebComponents, Workflow }.Any(s => solutionName.ToLower().Contains(s.ToLower()));
		}
	}

	class Const
	{
		internal const string TestsDirName = "Tests";
		internal const string ExecsDirName = "Execs";
		internal const string ModulesDirName = "Modules";
		internal const string SuiteDirName = "CP.Suite";
		internal const string AppServerDirName = "CP-Appserver";
		internal const string PlannerModuleDir = "Operations Planning";
	}
}