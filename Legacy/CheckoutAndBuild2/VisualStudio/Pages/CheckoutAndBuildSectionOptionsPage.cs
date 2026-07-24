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
	[Guid(GuidList.sectionOptionsPage)]
	public class CheckoutAndBuildSectionOptionsPage : DialogPage
    {
	    
        private readonly SettingsService settingsService;
		private OptionsSectionPageControl pageControl;
        
        /// <summary>
		/// Initializes a new instance of <see cref="T:Microsoft.VisualStudio.Shell.DialogPage"/>.
		/// </summary>
		public CheckoutAndBuildSectionOptionsPage()
		{
		    settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
		}


		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		protected override IWin32Window Window => (pageControl = new OptionsSectionPageControl { OptionsPage = this });

        protected override void OnActivate(CancelEventArgs e)
	    {
		    base.OnActivate(e);
			pageControl.Initialize();
	    }

	    /// <summary>
	    /// Handles Apply messages from the Visual Studio environment.
	    /// </summary>
	    /// <param name="e">[in] Arguments to event handler.</param>
	    protected override void OnApply(PageApplyEventArgs e)
	    {
		    base.OnApply(e);
			//CheckoutAndBuild2Package.GetGlobalService<TfsContext>().BuildDetailManager.SetEnabled(LoadBuildsForChangesets);
	    }
	}

}