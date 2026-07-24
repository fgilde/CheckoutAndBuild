using System;
using System.Windows.Input;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;

namespace FG.CheckoutAndBuild2.Common.Commands
{
	public class DelegateCommand<T> : NotificationObject, IUICommand         
	{
		#region Private Backing fields

		private readonly Action<T> executeMethod;
		private readonly Func<T, bool> canExecuteMethod;
		private string caption;
		private object tag;

		#endregion
		
		public object Tag
		{
			get { return tag; }
			set { SetProperty(ref tag, value); }
		}

	    public T Parameter { get; set; }

	    object IUICommand.Parameter => Parameter;
        
        public string Caption
		{
			get { return caption; }
			set { SetProperty(ref caption, value); }
		}

		private string inputGestureText;

		public string InputGestureText
		{
			get { return inputGestureText; }
			set { SetProperty(ref inputGestureText, value); }
		}

		public bool IsSeparator { get; set; }


		public DelegateCommand(string caption, Action<T> executeMethod, Func<T, bool> canExecuteMethod = null, ImageSource iconImage = null)
			: this(executeMethod, canExecuteMethod)
		{
			Caption = caption;
			IconImage = iconImage;
		}

		public DelegateCommand(Action<T> executeMethod, Func<T, bool> canExecuteMethod = null)
		{
			this.executeMethod = executeMethod;
			this.canExecuteMethod = canExecuteMethod ?? (arg => true);
		}

		/// <summary>
		/// Defines the method that determines whether the command can execute in its current state.
		/// </summary>
		/// <returns>
		/// true if this command can be executed; otherwise, false.
		/// </returns>
		/// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
		public bool CanExecute(object parameter)
		{
			return canExecuteMethod((T)parameter);
		}

		/// <summary>
		/// Defines the method to be called when the command is invoked.
		/// </summary>
		/// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
		public void Execute(object parameter)
		{
			 executeMethod((T)parameter);
		}

		public void RaiseCanExecuteChanged()
		{			
			var handler = CanExecuteChanged;
		    handler?.Invoke(this, new EventArgs());
		}

		public ImageSource IconImage { get; set; }

		public event EventHandler CanExecuteChanged;
	}

    public class SeparatorCommand : DelegateCommand<object>
    {
        public SeparatorCommand(): base("-", null)
        {
            IsSeparator = true;
        }
    }

    public interface IUICommand : ICommand
	{
		string Caption { get; }

		object Tag { get; }
		object Parameter { get; }

		string InputGestureText { get; }

		bool IsSeparator { get; set; }

		void RaiseCanExecuteChanged();

		ImageSource IconImage { get; }
	}

}