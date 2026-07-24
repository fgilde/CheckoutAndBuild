using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.Options
{
	/// <summary>
	/// Tools → Options stub page. The real (dynamic) settings UI lives in the tool window;
	/// this page just points there — no WinForms PropertyGrid.
	/// </summary>
	[ComVisible(true)]
	[Guid("9b7c3f1e-5d24-4a8e-b6f0-2c81d4a9e357")]
	public sealed class CheckoutAndBuildOptionsPage : UIElementDialogPage
	{
		private UIElement child;

		protected override UIElement Child => child ?? (child = CreateContent());

		private static UIElement CreateContent()
		{
			var panel = new StackPanel { Margin = new Thickness(8) };
			panel.Children.Add(new TextBlock
			{
				Text = "CheckoutAndBuild settings are edited in the CheckoutAndBuild tool window — "
					   + "globally via the gear button and per solution via the solution context menu.",
				TextWrapping = TextWrapping.Wrap
			});
			var button = new Button
			{
				Content = "Open CheckoutAndBuild Settings",
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0, 12, 0, 0),
				Padding = new Thickness(12, 4, 12, 4)
			};
			button.Click += (sender, e) => CheckoutAndBuildPackage.Instance?.ShowMainWindowSettings();
			panel.Children.Add(button);
			return panel;
		}
	}
}
