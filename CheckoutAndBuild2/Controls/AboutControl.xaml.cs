using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio.Pages;

namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl
    {
        public AboutControl()
        {
            InitializeComponent();
        }

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

	    private void linkClicked(object sender, RoutedEventArgs e)
	    {
		    var uri = ((AboutPage) DataContext).Url;
			if(!string.IsNullOrEmpty(uri))
				Process.Start(uri);
	    }
    }
}
