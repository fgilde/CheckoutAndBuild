using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Interop;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public class AutoEnabledElementHost : ElementHost
	{
		private const UInt32 DLGC_WANTARROWS = 0x0001;
		private const UInt32 DLGC_WANTTAB = 0x0002;
		private const UInt32 DLGC_WANTALLKEYS = 0x0004;
		private const UInt32 DLGC_HASSETSEL = 0x0008;
		private const UInt32 DLGC_WANTCHARS = 0x0080;
		private const UInt32 WM_GETDLGCODE = 0x0087;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Windows.Forms.Integration.ElementHost"/> class.
		/// </summary>
		public AutoEnabledElementHost()
		{
			ChildChanged += (sender, args) =>
			{
				if (Child != null)
				{					
					HwndSource s = PresentationSource.FromVisual(Child) as HwndSource;
					if (s != null)
						s.AddHook(ChildHwndSourceHook);
				}
			};
		}

		IntPtr ChildHwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WM_GETDLGCODE)
			{
				handled = true;
				return new IntPtr(DLGC_WANTCHARS | DLGC_WANTARROWS | DLGC_HASSETSEL);
			}
			return IntPtr.Zero;
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			SynchChildEnableState();

			base.OnEnabledChanged(e);
		}

		private void SynchChildEnableState()
		{
			IntPtr childHandle = GetWindow(Handle, GW_CHILD);
			if (childHandle != IntPtr.Zero)
			{
				EnableWindow(childHandle, Enabled);
			}
		}

		private const uint GW_CHILD = 5;

		[DllImport("user32.dll")]
		private extern static IntPtr GetWindow(IntPtr hWnd, uint uCmd);

		[DllImport("user32.dll")]
		private extern static bool EnableWindow(IntPtr hWnd, bool bEnable);
	}
}