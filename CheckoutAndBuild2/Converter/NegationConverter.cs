using System;
using System.Globalization;

namespace FG.CheckoutAndBuild2.Converter
{
	public class NegationConverter : BaseConverter<bool, bool>
	{
		public override bool Convert(bool value, Type targetType, object parameter, CultureInfo culture)
		{
			return !value;
		}
	}
}