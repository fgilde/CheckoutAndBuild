using System;
using System.Windows;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for ProfileSelector.xaml
	/// </summary>
	public partial class ProfileSelector : UserControl
	{
		internal ProfileSelectorViewModel Model => DataContext as ProfileSelectorViewModel;

	    public ProfileSelector()
		{
			InitializeComponent();
		}

		private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			Model.AddProfileCommand.Execute(null);
		}

		private void deleteProfileClick(object sender, RoutedEventArgs e)
		{
			Model.RemoveProfileCommand.Execute(Model.SelectedProfile);
		}
	}
}
