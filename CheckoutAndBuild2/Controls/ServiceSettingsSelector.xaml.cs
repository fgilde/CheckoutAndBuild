using System;
using System.Windows;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for BuildSettingsSelector.xaml
    /// </summary>
    public partial class ServiceSettingsSelector
    {
		public ServiceSettingsSelectorViewModel Model { get { return DataContext as ServiceSettingsSelectorViewModel; } }

		public ServiceSettingsSelector()
        {
            InitializeComponent();
        }

	    private void Popup_OnClosed(object sender, EventArgs e)
	    {
		    if (Model != null && Model.IsInPropertyEdit)
			    Model.IsInPropertyEdit = false;
	    }

	    private void optionsLinkClick(object sender, RoutedEventArgs e)
	    {
		    Model.Settings.ShowMainSettingsPage();
	    }
    }
}
