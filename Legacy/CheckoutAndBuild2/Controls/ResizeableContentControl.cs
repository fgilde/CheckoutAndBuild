using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FG.CheckoutAndBuild2.Controls
{

	/// <summary>
	/// 
	/// </summary>
	[TemplatePart(Type = typeof(Thumb), Name = PART_ResizeThumb)]
	public class ResizeableContentControl : ContentControl
	{
		const string PART_ResizeThumb = "PART_ResizeThumb";


		static ResizeableContentControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizeableContentControl), new FrameworkPropertyMetadata(typeof(ResizeableContentControl)));
		}

		private Thumb resizeThumb;


		/// <summary>
		/// When overridden in a derived class, is invoked whenever application code or internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate"/>.
		/// </summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			resizeThumb = (Thumb)GetTemplateChild(PART_ResizeThumb);
			resizeThumb.DragDelta += resizeThumb_DragDelta;
		}

		void resizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
			FrameworkElement parent = (FrameworkElement)Parent;

			double horizontalChange = e.HorizontalChange *-1;
			if (parent.Width + horizontalChange > parent.MinWidth)
				parent.Width += horizontalChange;
			if (parent.Height + e.VerticalChange > parent.MinHeight)
				parent.Height += e.VerticalChange;
		}
	}
}