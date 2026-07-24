using System;
using System.Globalization;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Converter
{
	public class VersionSpecToStringConverter: BaseConverter<VersionSpec,string>
	{
		public override string Convert(VersionSpec value, Type targetType, object parameter, CultureInfo culture)
		{
			return GetReadableString(value);
		}

		public static string GetReadableString(VersionSpec value)
		{
			if (value == null || value == VersionSpec.Latest || value.DisplayString == "T")
				return "Latest Version";
			return value.DisplayString;
		}
	}
}