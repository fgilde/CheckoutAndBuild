using System.Windows;
using System.Windows.Controls;

namespace FG.CheckoutAndBuild2.Common
{
	public abstract class BaseDataTemplateSelector<T> : DataTemplateSelector
	{
		/// <summary>
		/// When overridden in a derived class, returns a <see cref="T:System.Windows.DataTemplate"/> based on custom logic.
		/// </summary>
		/// <returns>
		/// Returns a <see cref="T:System.Windows.DataTemplate"/> or null. The default value is null.
		/// </returns>
		/// <param name="item">The data object for which to select the template.</param><param name="container">The data-bound object.</param>
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item is T)
				return SelectTemplate((T) item, container);
			return base.SelectTemplate(item, container);
		}

		public abstract DataTemplate SelectTemplate(T item, DependencyObject container);
	}
}