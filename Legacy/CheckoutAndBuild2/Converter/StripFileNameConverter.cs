using System;
using System.Globalization;
using System.IO;

namespace FG.CheckoutAndBuild2.Converter
{
	public class StripFileNameConverter : BaseConverter<string, string>
	{
		/// <summary>
		/// Converts the value of type <typeparamref name="TValue"/> to an value of type <typeparamref name="TResult"/>. 
		/// </summary>
		public override string Convert(string value, Type targetType, object parameter, CultureInfo culture)
		{
			return Path.GetFileName(value);
		}
	}
}