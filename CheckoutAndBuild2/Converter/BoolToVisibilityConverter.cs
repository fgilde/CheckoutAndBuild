using System;
using System.Globalization;
using System.Windows;

namespace FG.CheckoutAndBuild2.Converter
{
	public class BoolToVisibilityConverter : BaseConverter<bool, Visibility>
	{
		/// <summary>
		/// Converts the value of type to another type und jetzt fresse
		/// </summary>
		public override Visibility Convert(bool value, Type targetType, object parameter, CultureInfo culture)
		{
			if(parameter == null || System.Convert.ToBoolean(parameter))
				return value ? Visibility.Visible : Visibility.Collapsed;
			return value ? Visibility.Collapsed : Visibility.Visible;
		}
	}
}