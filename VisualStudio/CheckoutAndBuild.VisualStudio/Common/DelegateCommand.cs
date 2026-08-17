using System;
using System.Windows.Input;

namespace CheckoutAndBuild.VisualStudio.Common
{
	/// <summary>Minimal ICommand implementation (replaces the old FG.CheckoutAndBuild2 DelegateCommand).</summary>
	public class DelegateCommand : ICommand
	{
		private readonly Action<object> executeMethod;
		private readonly Func<object, bool> canExecuteMethod;

		public DelegateCommand(Action executeMethod, Func<bool> canExecuteMethod = null)
			: this(_ => executeMethod(), canExecuteMethod == null ? (Func<object, bool>)null : _ => canExecuteMethod())
		{
		}

		public DelegateCommand(Action<object> executeMethod, Func<object, bool> canExecuteMethod = null)
		{
			this.executeMethod = executeMethod ?? throw new ArgumentNullException(nameof(executeMethod));
			this.canExecuteMethod = canExecuteMethod ?? (_ => true);
		}

		public bool CanExecute(object parameter) => canExecuteMethod(parameter);

		public void Execute(object parameter) => executeMethod(parameter);

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
	}
}
