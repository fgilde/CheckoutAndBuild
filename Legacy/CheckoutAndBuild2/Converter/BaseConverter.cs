using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FG.CheckoutAndBuild2.Converter
{
	public abstract class BaseConverter<TValue, TResult> : DependencyObject, IValueConverter
	{
		public virtual bool AllowNullValues { get { return false;} }

		/// <summary>
		/// Converts the value of type <typeparamref name="TValue"/> to an value of type <typeparamref name="TResult"/>. 
		/// </summary>
		public abstract TResult Convert(TValue value, Type targetType, object parameter, CultureInfo culture);

		/// <summary>
		/// Converts the value of type <typeparamref name="TResult"/> back to its old value of type <typeparamref name="TValue"/>. 
		/// </summary>
		public virtual TValue ConvertBack(TResult value, Type targetType, object parameter, CultureInfo culture)
		{
			return default(TValue);
		}

		/// <summary>
		/// Converts a value. 
		/// </summary>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TValue || (value == null && AllowNullValues))
				return Convert(value != (object) default(TValue) ? (TValue)value : default(TValue), targetType, parameter, culture);
			return default(TResult);
		}

		/// <summary>
		/// Converts a value back. 
		/// </summary>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TResult || (value == null && AllowNullValues))
				return ConvertBack(value != (object)default(TResult) ? (TResult)value : default(TResult), targetType, parameter, culture);
			return default(TValue);
		}
	}
}