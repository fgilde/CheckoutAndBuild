using System;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace FG.CheckoutAndBuild2.Common
{
    internal class PopupStaysOpenScope : IDisposable
    {
        private readonly DispatcherTimer pollingTimer;

        public PopupStaysOpenScope(Popup popup)
        {
	        if (popup != null)
	        {
		        popup.StaysOpen = true;
		        pollingTimer = new DispatcherTimer(DispatcherPriority.Normal)
		        {
			        Interval = TimeSpan.FromMilliseconds(500)
		        };

		        pollingTimer.Tick += (obj, e) =>
		        {
					popup.StaysOpen = false;
					popup.IsOpen = true;
					pollingTimer.Stop();
		        };
	        }
        }

        public void Dispose()
        {
			if(pollingTimer != null)
				pollingTimer.Start();
        }
    }
}