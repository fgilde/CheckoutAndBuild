using System;
using System.Windows;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Themes
{
	public class ProjectViewModelTemplateSelector : BaseDataTemplateSelector<ProjectViewModel>
	{
		private const string defaultTemplateName = "ProjectViewModelDefaultTemplate";
		private const string operationTemplateName = "ProjectViewModelOperationTemplate";

		/// <summary>
		/// When overridden in a derived class, returns a <see cref="T:System.Windows.DataTemplate" /> based on custom logic.
		/// </summary>
		/// <param name="item">The data object for which to select the template.</param>
		/// <param name="container">The data-bound object.</param>
		/// <returns>
		/// Returns a <see cref="T:System.Windows.DataTemplate" /> or null. The default value is null.
		/// </returns>
		public override DataTemplate SelectTemplate(ProjectViewModel item, DependencyObject container)
		{
			if (item != null)
			{
				if (item.CurrentOperation != Operations.None)
					return Application.Current.TryFindResource(operationTemplateName) as DataTemplate;
				return Application.Current.TryFindResource(defaultTemplateName) as DataTemplate;
			}
			return null;
		}
	}

}