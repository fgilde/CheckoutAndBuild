using System.Collections.Generic;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace FG.CheckoutAndBuild2.Behavoir
{
	public class PropertyTooltipBehavior :Behavior<FrameworkElement>
	{
		private object defaultTip = null;
		
		#region Statics

		/// <summary>
		/// PropertyNameToFilterProperty
		/// </summary>
		public static readonly DependencyProperty TooltipPropertiesProperty =
			DependencyProperty.RegisterAttached("TooltipProperties", typeof(string), typeof(PropertyTooltipBehavior), new UIPropertyMetadata(string.Empty));

		/// <summary>
		/// returns the PropertyNameToFilterProperty as string.
		/// </summary>
		public static string GetPropertyNameToFilter(DependencyObject obj)
		{
			return (string) obj.GetValue(TooltipPropertiesProperty);
		}

		/// <summary>
		/// Sets the PropertyNameToFilterProperty.
		/// </summary>
		public static void SetPropertyNameToFilter(DependencyObject obj, string value)
		{
			obj.SetValue(TooltipPropertiesProperty, value);
		}

		/// <summary>
		/// Eigenschaft, auf die der Filter greift
		/// </summary>
		public string TooltipProperties
		{
			get { return GetPropertyNameToFilter(AssociatedObject); }
			set { SetPropertyNameToFilter(AssociatedObject, value); }
		}

		#endregion

		public IEnumerable<string> GetProperties()
		{
			return TooltipProperties.Split(',');
		}

		public object GetTooltip()
		{
			// TODO: Generate Tooltip
			return defaultTip; 
		}

		/// <summary>
		/// Called when [attached].
		/// </summary>
		protected override void OnAttached()
		{
			base.OnAttached();
			if (AssociatedObject != null)
			{
				defaultTip = AssociatedObject.ToolTip;
				AssociatedObject.ToolTip = GetTooltip();
			}
		}

		/// <summary>
		/// Called when [detaching].
		/// </summary>
		protected override void OnDetaching()
		{
			base.OnDetaching();
			if (AssociatedObject != null)
			{
				AssociatedObject.ToolTip = defaultTip;				
			}
		}

	}
}