using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FG.CheckoutAndBuild2.Converter
{
	public class StripTagsConverter : BaseConverter<string, string>
	{
		/// <summary>
		/// Converts the value of type <typeparamref name="TValue"/> to an value of type <typeparamref name="TResult"/>. 
		/// </summary>
		public override string Convert(string value, Type targetType, object parameter, CultureInfo culture)
		{			
			var maxLength = parameter as int? ?? System.Convert.ToInt32(parameter);
			string result = Regex.Replace(value, @"<[^>]*>", String.Empty);
			if(maxLength > 0 && result.Length > maxLength)
				result = new string(result.Take(maxLength).ToArray()) + "...";
			return result;			
		}
	}
}