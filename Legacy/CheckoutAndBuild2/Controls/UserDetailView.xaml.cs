using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for UserDetailView.xaml
	/// </summary>
	public partial class UserDetailView
	{
		public UserDetailView()
		{
			InitializeComponent();
		}

		private UserDetailViewModel UserDetailViewModel
		{
			get
			{
				return DataContext as UserDetailViewModel;
			}
		}

		private void MailLink_OnClick(object sender, RoutedEventArgs e)
		{
			var m = UserDetailViewModel;
			if (m != null && !string.IsNullOrEmpty(m.Email))
				Process.Start("mailto:" + m.Email);
		}
		
		//protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		//{
		//	if (!e.Handled)
		//		this.ReRouteMouseWheelEventToParent(e);
		//	base.OnPreviewMouseWheel(e);
		//}

		private void NameLink_OnClick(object sender, RoutedEventArgs e)
		{
			var m = DataContext as UserDetailViewModel;
			TeamControlFactory.ShowUserIdentityInfoDialog(m.Identity);
		}	
	}
}
