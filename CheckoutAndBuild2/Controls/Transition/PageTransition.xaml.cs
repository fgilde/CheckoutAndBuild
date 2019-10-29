using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace FG.CheckoutAndBuild2.Controls.Transition
{
    /// <summary>
    /// Seitenübergänge
    /// </summary>
    public partial class PageTransition
    {
        private readonly Stack<FrameworkElement> pages = new Stack<FrameworkElement>();
	    private Storyboard hidePageStoryboard;
	    private Storyboard showPageStoryboard;
        #region Dependency Properties

		/// <summary>
		/// <see cref="IsAnimationRunning"/>
		/// </summary>
		public static readonly DependencyProperty IsAnimationRunningProperty =
			DependencyProperty.Register("IsAnimationRunning", typeof(bool), typeof(PageTransition), new PropertyMetadata(false));

        /// <summary>
        /// <see cref="TransitionType"/>
        /// </summary>
        public static readonly DependencyProperty TransitionTypeProperty = DependencyProperty.Register("TransitionType", typeof(PageTransitionType), typeof(PageTransition), new PropertyMetadata(PageTransitionType.SlideAndFade));

        /// <summary>
        /// <see cref="AnimationDuration"/>
        /// </summary>
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register("AnimationDuration", typeof(Duration), typeof(PageTransition), new UIPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(75))));

        /// <summary>
        /// <see cref="FadeInDuration"/>
        /// </summary>
        public static readonly DependencyProperty FadeInDurationProperty =
            DependencyProperty.Register("FadeInDuration", typeof(Duration), typeof(PageTransition), new UIPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(25))));


        /// <summary>
        /// <see cref="FadeOutDuration"/>
        /// </summary>
        public static readonly DependencyProperty FadeOutDurationProperty =
            DependencyProperty.Register("FadeOutDuration", typeof(Duration), typeof(PageTransition), new UIPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(50))));


        #endregion

		/// <summary>
		/// Gibt an, ob aktuell eine Animation läuft
		/// </summary>
		public bool IsAnimationRunning
		{
			get { return (bool)GetValue(IsAnimationRunningProperty); }
			private set { SetValue(IsAnimationRunningProperty, value); }
		}

        /// <summary>
        /// Occurs when [animation completed].
        /// </summary>
        public event EventHandler<EventArgs> AnimationCompleted;



        /// <summary>
        /// Dauer der Animation
        /// </summary>
        public Duration AnimationDuration
        {
            get { return (Duration)GetValue(AnimationDurationProperty); }
            set { SetValue(AnimationDurationProperty, value); }
        }


        /// <summary>
        /// Dauer des Einfadens
        /// </summary>
        public Duration FadeInDuration
        {
            get { return (Duration)GetValue(FadeInDurationProperty); }
            set { SetValue(FadeInDurationProperty, value); }
        }

        /// <summary>
        /// Dauer des Ausfadens
        /// </summary>
        public Duration FadeOutDuration
        {
            get { return (Duration)GetValue(FadeOutDurationProperty); }
            set { SetValue(FadeOutDurationProperty, value); }
        }

        /// <summary>
        /// Die aktuelle Seite
        /// </summary>
        public FrameworkElement CurrentPage { get; set; }

        /// <summary>
        /// Übergangsart
        /// </summary>
        public PageTransitionType TransitionType
        {
            get { return (PageTransitionType)GetValue(TransitionTypeProperty); }
            set { SetValue(TransitionTypeProperty, value); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageTransition"/> class.
        /// </summary>
        public PageTransition()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Zeigt das übergebene FrameworkElement mit der aktuellen gesetzten Übergangsart an
        /// </summary>
        public void ShowPage(FrameworkElement newPage)
        {
			if (IsAnimationRunning)
			{
				if (hidePageStoryboard != null)
				{
					hidePageStoryboard.Completed -= HidePageStoryboardCompleted;
				}
				contentPresenter.Content = null;
				IsAnimationRunning = false;
			}

			pages.Push(newPage);
			Task.Factory.StartNew(ShowNewPage);
        }

	    public void HidePage(FrameworkElement newPage)
	    {
			ShowPage(null);
	    }

	    void ShowNewPage()
        {
            Dispatcher.Invoke(delegate
            {
				if (contentPresenter.Content != null)
				{
					var oldPage = contentPresenter.Content as FrameworkElement;

					if (oldPage != null)
					{
						oldPage.Loaded -= NewPageLoaded;

						UnloadPage(oldPage);
					}
				}
				else
				{
					ShowNextPage();
				}

            });
        }

        void ShowNextPage()
        {
            FrameworkElement newPage = pages.Pop();

			if(newPage != null)
				newPage.Loaded += NewPageLoaded;

            contentPresenter.Content = newPage;
        }

	    public void UnloadPage(FrameworkElement page)
        {
            if (TransitionType != PageTransitionType.None)
            {
                hidePageStoryboard = ((Storyboard)Resources[string.Format("{0}Out", TransitionType.ToString())]).Clone();
                hidePageStoryboard.Completed += HidePageStoryboardCompleted;
				IsAnimationRunning = true;
                hidePageStoryboard.Begin(contentPresenter);
            }
            else
            {
                HidePageStoryboardCompleted(this, EventArgs.Empty);
            }
        }

        void NewPageLoaded(object sender, RoutedEventArgs e)
        {
            if (TransitionType != PageTransitionType.None)
            {

                showPageStoryboard = (Storyboard)Resources[string.Format("{0}In", TransitionType.ToString())];
				showPageStoryboard.Completed += (o, args) =>
                                             {
												 IsAnimationRunning = false;
                                                 RaiseCompleted();
                                             };
                IsAnimationRunning = true;
				showPageStoryboard.Begin(contentPresenter);
            }
            CurrentPage = sender as FrameworkElement;
        }

        void HidePageStoryboardCompleted(object sender, EventArgs e)
        {
            contentPresenter.Content = null;
			IsAnimationRunning = false;

            ShowNextPage();
        }

        void RaiseCompleted()
        {
            EventHandler<EventArgs> handler = AnimationCompleted;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

	public class CenterConverter : IValueConverter
	{
		/// <summary>
		/// Converts a value.
		/// </summary>
		/// <param name="value">The value produced by the binding source.</param>
		/// <param name="targetType">The type of the binding target property.</param>
		/// <param name="parameter">The converter parameter to use.</param>
		/// <param name="culture">The culture to use in the converter.</param>
		/// <returns>
		/// A converted value. If the method returns null, the valid null value is used.
		/// </returns>
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			double x = 2;
			if (parameter != null)
			{
				if (!double.TryParse(parameter.ToString(), out x))
					x = 2;
			}
			return (double)value / x;
		}

		/// <summary>
		/// Converts a value.
		/// </summary>
		/// <param name="value">The value that is produced by the binding target.</param>
		/// <param name="targetType">The type to convert to.</param>
		/// <param name="parameter">The converter parameter to use.</param>
		/// <param name="culture">The culture to use in the converter.</param>
		/// <returns>
		/// A converted value. If the method returns null, the valid null value is used.
		/// </returns>
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			double x = 2;
			if (parameter != null)
			{
				if (!double.TryParse(parameter.ToString(), out x))
					x = 2;
			}
			return (double)value * x;
		}
	}

}
