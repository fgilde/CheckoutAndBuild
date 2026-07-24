using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Services;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [CLSCompliant(false), ComVisible(true)]
	[Guid(GuidList.pluginOptionsPage)]	
	public class CheckoutAndBuildPluginsOptionsPage : DialogPage
    {
	    
        private readonly SettingsService settingsService;
		private OptionsPluginPageControl pageControl;
        
        /// <summary>
		/// Initializes a new instance of <see cref="T:Microsoft.VisualStudio.Shell.DialogPage"/>.
		/// </summary>
		public CheckoutAndBuildPluginsOptionsPage()
		{
		    settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
		}


		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		protected override IWin32Window Window { get { return (pageControl = new OptionsPluginPageControl { OptionsPage = this }); } }

	    protected override void OnActivate(CancelEventArgs e)
	    {
		    base.OnActivate(e);
			pageControl.Initialize();
	    }

	}

}