using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	[Guid("6f4f9b95-2c1e-4a5a-9d3e-0b7a4d9c3f21")]
	public class WorkItemToolWindow : ToolWindowPane
	{
		public WorkItemToolWindow() : base(null)
		{
			Caption = "CheckoutAndBuild Work Items";
			Content = new WorkItemToolWindowControl();
		}
	}
}
