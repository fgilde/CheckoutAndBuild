using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Controls.Forms;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [CLSCompliant(false), ComVisible(true)]
	[Guid(GuidList.copySettingsOptionsPage)]
	public class CheckoutAndBuildCopySettingsPage : DialogPage
    {
	    private CopySettingsControl pageControl;        
        

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		protected override IWin32Window Window => pageControl ?? (pageControl = new CopySettingsControl());

        /// <summary>
	    /// Handles Windows Activate messages from the Visual Studio environment.
	    /// </summary>
	    /// <param name="e">[in] Arguments to event handler.</param>
	    protected override void OnActivate(CancelEventArgs e)
	    {
		    base.OnActivate(e);
			pageControl.Initialize();
	    }

	}

}