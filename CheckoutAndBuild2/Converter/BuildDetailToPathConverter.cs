using System;
using System.Globalization;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation.Build.Client;

namespace FG.CheckoutAndBuild2.Converter
{
	public class BuildDetailToPathConverter : BaseConverter<IBuildDetail, Geometry>
	{
		public override bool AllowNullValues { get { return true; } }

		public override Geometry Convert(IBuildDetail value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return Pathes.Museum; // TODO: Loading
			if (!value.BuildFinished || value.Status == BuildStatus.InProgress)
				return Pathes.Execute;
			if (value.Status == BuildStatus.Succeeded)
				return Pathes.Success;
			return Pathes.Error;
		}
	}

	public class BuildDetailToBrushConverter : BaseConverter<IBuildDetail, Brush>
	{
		public override bool AllowNullValues { get { return true; } }
		public override Brush Convert(IBuildDetail value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return Brushes.Transparent;
			if (value.BuildFinished)
			{
				if (value.Status == BuildStatus.Succeeded)
					return Brushes.Green;
				if (value.Status == BuildStatus.Failed)
					return Brushes.Red;				
			}
			return Brushes.Orange;
		}
	}
}