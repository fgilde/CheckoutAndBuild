using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.VisualStudio.PlatformUI;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for WorkingFolderTree.xaml
	/// </summary>
	public partial class WorkingFolderTree
	{
		#region dll Import mouse simulate

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

		private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
		private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
		private const int MOUSEEVENTF_LEFTUP = 0x0004;
		private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
		private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
		private const int MOUSEEVENTF_MOVE = 0x0001;
		private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
		private const int MOUSEEVENTF_RIGHTUP = 0x0010;
		private const int MOUSEEVENTF_WHEEL = 0x0800;
		private const int MOUSEEVENTF_XDOWN = 0x0080;
		private const int MOUSEEVENTF_XUP = 0x0100;

		#endregion
		
		public static readonly DependencyProperty TitleProperty =
			DependencyProperty.Register("Title", typeof(string), typeof(WorkingFolderTree), new PropertyMetadata(string.Empty));

		public WorkingFolderListViewModel Model { get { return DataContext as WorkingFolderListViewModel; } }
		

		public WorkingFolderTree()
		{
			InitializeComponent();
		}

		public string Title
		{
			get { return (string)GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}

		
		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

		private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
		{
			loader.Visibility = Visibility.Collapsed;
		}

		private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs args)
		{
			var list = sender as ListBox;
			
			if (list?.ContextMenu != null)
			{
				var wpfPoint = PointToScreen(args.GetPosition(list));
				
				var focused = FocusManager.GetFocusedElement(Application.Current.MainWindow);
				if (focused is TextBox)
					return;

				StaticCommands.RaiseAllCanExecuteChanged();

				List<Control> itemsToHide = list.ContextMenu.Items.OfType<Control>().Where(control => (!(control.Tag is MenuItemSettings) || !((MenuItemSettings)control.Tag).IsVisibleInQuicklist ) && control.Visibility == Visibility.Visible).ToList();
				itemsToHide.Apply(control => control.Visibility = Visibility.Collapsed);
				RoutedEventHandler handler = (o, eventArgs) => itemsToHide.Apply(control => control.Visibility = Visibility.Visible);
				list.ContextMenu.Closed += handler;
				list.ContextMenu.Closed += (o, eventArgs) =>
				{
					list.ContextMenu.Closed -= handler;
				};				
				//list.ContextMenu.IsOpen = true;							
				mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, (int) wpfPoint.X, (int) wpfPoint.Y, 0, 0);
			}
		}		
	}
}
