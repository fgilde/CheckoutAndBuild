using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common.Commands;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;

namespace FG.CheckoutAndBuild2.Extensions
{
	public static class WpfControlExtensions
	{

		/// <summary>
		/// Erstellt aus einem IUICommand ein MenuIten
		/// </summary>
		public static object ToMenuItemCore(this IUICommand command, Func<IUICommand, string> getCaptionFunc = null )
		{
			if (getCaptionFunc == null)
				getCaptionFunc = c => c.Caption;
			if (command.IsSeparator || string.IsNullOrWhiteSpace(command.Caption) || command.Caption == "-")
				return new Separator();
			var result = new MenuItem { Header = getCaptionFunc(command).Replace("_", "__"), Command = command, Tag = command.Tag, InputGestureText = command.InputGestureText };
            if(command.Parameter != null)
                result.CommandParameter = command.Parameter;
            if (command.IconImage != null)
			{
				var imgControl = new Image { Source = command.IconImage, Height = 16, Width = 16 };
				result.Icon = imgControl;
			}
			//result.IsChecked = command.IsChecked.Value;

			return result;
		}

		/// <summary>
		/// Erstellt aus allen IUICommands ein MenuIten
		/// </summary>
		public static IEnumerable<object> ToMenuItemsCore(this IEnumerable<IUICommand> commands, Func<IUICommand, string> getCaptionFunc = null)
		{
			return commands.Select(c => c.ToMenuItemCore(getCaptionFunc));
		}

		/// <summary>
		/// Erstellt aus einem IUICommand ein MenuIten
		/// </summary>
		public static T ToMenuItemGeneric<T>(this IUICommand command) where T : class
		{
			var result = new MenuItem { Header = command.Caption.Replace("_", "__"), Command = command };
			return result as T;
		}

		/// <summary>
		/// Erstellt aus allen IUICommands ein MenuIten
		/// </summary>
		public static IEnumerable<T> ToMenuItemsGeneric<T>(this IEnumerable<IUICommand> commands) where T : class
		{
			return commands.Select(c => c.ToMenuItemGeneric<T>());
		}

		public static MenuItem ToMenuItem(this IUICommand command, Func<IUICommand, string> getCaptionFunc = null)
		{
			return command.ToMenuItemCore(getCaptionFunc) as MenuItem;
		}

		public static IEnumerable<MenuItem> ToMenuItems(this IEnumerable<IUICommand> commands, Func<IUICommand, string> getCaptionFunc = null)
		{
			return ToMenuItemsCore(commands, getCaptionFunc).Cast<MenuItem>();
		}

		/// <summary>
		/// Erstellt aus allen IUICommands ein MenuIten und gibt ein entsprechendes Contextmenu zurück
		/// </summary>
		public static ContextMenu ToContextMenu(this IEnumerable<IUICommand> commands, Func<IUICommand, string> getCaptionFunc = null)
		{
			var result = new ContextMenu();
			foreach (IUICommand uiCommand in commands.Where(command => command != null))
			{
				if(uiCommand.IsSeparator)
					result.Items.Add(new Separator());
				else
					result.Items.Add(uiCommand.ToMenuItem(getCaptionFunc));
			}
			return result;
		}

		public static TextLink ToTextLink(this IUICommand command)
		{
			return new TextLink {Text = command.Caption, Command = command, Height = 20};
		}

		public static void ReRouteMouseWheelEventToParent(this FrameworkElement frameworkElement, MouseWheelEventArgs e)
		{
			UIElement uiElement = frameworkElement.Parent as UIElement ?? frameworkElement.FindAncestor<UIElement>();
			if (uiElement != null)
			{
				e.Handled = true;
				MouseWheelEventArgs mouseWheelEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = frameworkElement
				};
				uiElement.RaiseEvent(mouseWheelEventArgs);
			}
		}

	}
}