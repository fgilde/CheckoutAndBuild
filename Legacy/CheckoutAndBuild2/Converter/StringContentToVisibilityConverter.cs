using System;
using System.Globalization;
using System.Windows;

namespace FG.CheckoutAndBuild2.Converter
{
	public class StringContentToVisibilityConverter : BaseConverter<string, Visibility>
	{
		/// <summary>
		/// Converts the value of type TValue to an value of type"TResult". 
		/// </summary>
		public override Visibility Convert(string value, Type targetType, object parameter, CultureInfo culture)
		{
			return new BoolToVisibilityConverter().Convert(string.IsNullOrWhiteSpace(value), targetType, parameter, culture);
		}
	}
}