using System;
using System.Windows.Data;

namespace FG.CheckoutAndBuild2.Converter
{
	public class IsFocusAndNotEmptyToVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool visible = true;
			foreach (object value in values)
			{
				if (value is bool)
					visible = visible && (bool) value;
				if(value is string)
					visible = visible && !string.IsNullOrEmpty(value.ToString());
			}

			return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
		}

		public object[] ConvertBack(object value,
									Type[] targetTypes,
									object parameter,
									System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}