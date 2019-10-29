using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common.Commands;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for SearchBox.xaml
	/// </summary>
	public class SearchBox : TextBox
	{
		public ICommand ClearCommand { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Windows.Controls.TextBox"/> class.
		/// </summary>
		public SearchBox()
		{
			ClearCommand = new DelegateCommand<object>(o => Text = string.Empty);
		}

		static SearchBox()
		{			
			DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchBox), new FrameworkPropertyMetadata(typeof(SearchBox)));
			//TextProperty.OverrideMetadata(typeof(SearchBox), new FrameworkPropertyMetadata(new PropertyChangedCallback(TextPropertyChanged)));
		}	
	}
}
