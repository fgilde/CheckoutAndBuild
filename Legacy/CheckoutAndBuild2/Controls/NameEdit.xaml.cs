using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Client;


namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for NameEdit.xaml
	/// </summary>
	public partial class NameEdit
	{
		private readonly Action<NameEdit, string> onOkAction;
		private readonly Action<NameEdit> onCancelAction;

        #region Dependency
	    // Using a DependencyProperty as the backing store for ExtraCommandsMenu.  This enables animation, styling, binding, etc...
	    public static readonly DependencyProperty ExtraCommandsMenuProperty =
	        DependencyProperty.Register("ExtraCommandsMenu", typeof(ContextMenu), typeof(NameEdit), new PropertyMetadata(null));



        // Using a DependencyProperty as the backing store for CanAccept.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CanAcceptProperty =
			DependencyProperty.Register("CanAccept", typeof(bool), typeof(NameEdit), new PropertyMetadata(false));
		public static readonly DependencyProperty IsReadOnlyProperty =
			DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(NameEdit), new PropertyMetadata(false));

		
		public static readonly DependencyProperty WatermarkProperty =
			DependencyProperty.Register("Watermark", typeof (string), typeof (NameEdit),
				new PropertyMetadata("Enter a Name <Required>"));

		public static readonly DependencyProperty AcceptTextProperty =
			DependencyProperty.Register("AcceptText", typeof (string), typeof (NameEdit), new PropertyMetadata("OK"));

		public static readonly DependencyProperty CancelTextProperty =
			DependencyProperty.Register("CancelText", typeof (string), typeof (NameEdit), new PropertyMetadata("Cancel"));

        public static readonly DependencyProperty CheckboxTextProperty =
            DependencyProperty.Register("CheckboxText", typeof(string), typeof(NameEdit), new PropertyMetadata(""));

        // Using a DependencyProperty as the backing store for HasCheckbox.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HasCheckboxProperty =
            DependencyProperty.Register("HasCheckbox", typeof(bool), typeof(NameEdit), new PropertyMetadata(false));

       
        // Using a DependencyProperty as the backing store for IsChecked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(NameEdit), new PropertyMetadata(false));


        #endregion
        
        public NameEdit(Action<NameEdit, string> onOkAction, Action<NameEdit> onCancelAction = null)
		{			
			this.onOkAction = onOkAction;
			this.onCancelAction = onCancelAction;
			AcceptCommand = new DelegateCommand<object>(ExecuteAcceptMethod, CanExecuteAcceptMethod);
			CancelCommand = new DelegateCommand<object>(ExecuteCancelMethod);
			Loaded += (sender, args) => SetFocus();
			InitializeComponent();
			DataContext = this;
		}
	    
        public ContextMenu ExtraCommandsMenu
        {
            get { return (ContextMenu)GetValue(ExtraCommandsMenuProperty); }
            set { SetValue(ExtraCommandsMenuProperty, value); }
        }

        public bool CanAccept
		{
			get { return (bool)GetValue(CanAcceptProperty); }
			private set { SetValue(CanAcceptProperty, value); }
		}

		public DelegateCommand<object> AcceptCommand { get; set; }
		public DelegateCommand<object> CancelCommand { get; set; }

		public bool IsReadOnly
		{
			get { return (bool)GetValue(IsReadOnlyProperty); }
			set { SetValue(IsReadOnlyProperty, value); }
		}

		public string Value
		{
			get { return valueTextBox.Text; } 
			set { valueTextBox.Text = value; } 
		}

		public string CancelText
		{
			get { return (string)GetValue(CancelTextProperty); }
			set { SetValue(CancelTextProperty, value); }
		}

        public string CheckboxText
        {
            get { return (string)GetValue(CheckboxTextProperty); }
            set { SetValue(CheckboxTextProperty, value); }
        }

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public bool HasCheckbox
        {
            get { return (bool)GetValue(HasCheckboxProperty); }
            set { SetValue(HasCheckboxProperty, value); }
        }


        public string Watermark
		{
			get { return (string)GetValue(WatermarkProperty); }
			set { SetValue(WatermarkProperty, value); }
		}


		public string AcceptText
		{
			get { return (string)GetValue(AcceptTextProperty); }
			set { SetValue(AcceptTextProperty, value); }
		}

		public void Close()
		{
			var model = CheckoutAndBuild2Package.GetGlobalService<MainViewModel>();
			if (model != null && model.ExtraContent == this)
				model.ExtraContent = null;
		}

		private void ExecuteCancelMethod(object obj)
		{
		    onCancelAction?.Invoke(this);
		    Close();
		}

		public void SetFocus()
		{
			valueTextBox.Focus();			
		}

		private bool CanExecuteAcceptMethod(object o)
		{
			return (CanAccept = (IsReadOnly || !string.IsNullOrEmpty(Value)));
		}		

		private void ExecuteAcceptMethod(object o)
		{
		    onOkAction?.Invoke(this, Value);
		    Close();
		}

		private void ValueTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
		{
			watermark.Visibility = !IsReadOnly && string.IsNullOrEmpty(Value) ? Visibility.Visible : Visibility.Collapsed;
			AcceptCommand.RaiseCanExecuteChanged();
			CanAccept = CanExecuteAcceptMethod(null);
		}

		private void ValueTextBox_OnKeyDown(object sender, KeyEventArgs e)
		{
			if(e.Key == Key.Escape)
				ExecuteCancelMethod(null);
			if (e.Key == Key.Enter && CanExecuteAcceptMethod(null))
				ExecuteAcceptMethod(null);
		}

	    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
	    {
	        ExtraCommandsMenu.IsOpen = true;
	    }
	}
}
