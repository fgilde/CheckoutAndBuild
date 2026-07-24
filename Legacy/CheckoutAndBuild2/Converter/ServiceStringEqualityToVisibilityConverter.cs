using System;
using System.Globalization;
using System.Windows;
using CheckoutAndBuild2.Contracts.Service;

namespace FG.CheckoutAndBuild2.Converter
{
	public class ServiceStringEqualityToVisibilityConverter : BaseConverter<IOperationService, Visibility>
	{
		/// <summary>
		/// 
		/// </summary>
		public override Visibility Convert(IOperationService value, Type targetType, object parameter, CultureInfo culture)
		{
			if(parameter == null || value == null)
				return Visibility.Visible;
			return value.ServiceId.ToString().ToLower() == parameter.ToString().ToLower()
				? Visibility.Visible
				: Visibility.Collapsed;
		}
	}
}