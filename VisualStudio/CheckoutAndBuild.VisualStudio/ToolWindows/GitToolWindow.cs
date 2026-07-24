using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	[Guid("1b14d13c-a5c3-47a8-8c00-ff79098a1202")]
	public class GitToolWindow : ToolWindowPane
	{
		public GitToolWindow() : base(null)
		{
			Caption = "CheckoutAndBuild Git";
			Content = new GitToolWindowControl();
		}
	}
}
