using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Controls;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace FG.CheckoutAndBuild2.Converter
{
	public class SolutionListItemIconConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{		
			try
			{
				if (values.Length != 2 || !(values[0] is ProjectViewModel) || !(values[1] is Color))
					return (object)null;
				var explorerSccSolutionInfo = (ProjectViewModel)values[0];
				Color backgroundColor = (Color)values[1];
				bool themeIcon;
				BitmapSource bitmapSourceForFile = TeamFoundationIconStripHelper.Instance.GetBitmapSourceForFile(explorerSccSolutionInfo.ItemPath, out themeIcon);
				if (themeIcon)
					return (object)TeamFoundationIconStripHelper.Instance.ThemeBitmapSource(bitmapSourceForFile, backgroundColor);
				else
					return (object)bitmapSourceForFile;
			}
			catch (Exception ex)
			{
				TeamFoundationTrace.TraceException(TraceKeywordSets.TeamExplorer, ex);
			}
			return (object)null;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return (object[])null;
		}
	}
}