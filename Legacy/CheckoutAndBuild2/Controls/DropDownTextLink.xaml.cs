using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for DropDownTextLink.xaml
	/// </summary>
	public partial class DropDownTextLink
	{
		// Using a DependencyProperty as the backing store for CommandParameter.  This enables animation, styling, binding, etc...

		public static readonly DependencyProperty CommandParameterProperty =
			DependencyProperty.Register("CommandParameter", typeof(object), typeof(DropDownTextLink), new PropertyMetadata(null));

		// Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...

		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register("Text", typeof(string), typeof(DropDownTextLink), new PropertyMetadata(string.Empty));

		// Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...

		public static readonly DependencyProperty CommandProperty =
			DependencyProperty.Register("Command", typeof(ICommand), typeof(DropDownTextLink), new PropertyMetadata(null));


		public object CommandParameter
		{
			get { return (object)GetValue(CommandParameterProperty); }
			set { SetValue(CommandParameterProperty, value); }
		}

		public ICommand Command
		{
			get { return (ICommand)GetValue(CommandProperty); }
			set { SetValue(CommandProperty, value); }
		}

		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}

		private readonly Brush foreground; 

		public DropDownTextLink()
		{
			InitializeComponent();
			foreground = TextBlock.Foreground;
		}

		private void Hyperlink_OnMouseEnter(object sender, MouseEventArgs e)
		{
			TextBlock.TextDecorations = TextDecorations.Underline;
			Hyperlink.Foreground = TextBlock.Foreground = foreground;
		}

		private void Hyperlink_OnMouseLeave(object sender, MouseEventArgs e)
		{
			TextBlock.TextDecorations = null;
		}
	}
}
