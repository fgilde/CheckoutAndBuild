using System;
using System.Globalization;
using System.Windows;

namespace FG.CheckoutAndBuild2.Converter
{
	public class BoolToFontWeightConverter : BaseConverter<bool, FontWeight>
	{
		public override FontWeight Convert(bool value, Type targetType, object parameter, CultureInfo culture)
		{
			return value ? FontWeights.Bold : FontWeights.Normal;
		}
	}
}