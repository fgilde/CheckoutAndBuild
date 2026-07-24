using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	[Guid("ce5d9838-a362-4f99-9c58-aa5e561afcb4")]
	public class MainToolWindow : ToolWindowPane
	{
		public MainToolWindow() : base(null)
		{
			Caption = "CheckoutAndBuild";
			Content = new MainToolWindowControl();
		}
	}
}
