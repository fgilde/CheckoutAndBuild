using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.TeamFoundation.Client;

namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for LoadingWait.xaml
    /// </summary>
    public partial class LoadingWait
    {
        private readonly List<Ellipse> ellipses;
        private readonly DispatcherTimer animationTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadingWait"/> class.
        /// </summary>
        public LoadingWait()
        {
            DataContext = this;

            InitializeComponent();

            ellipses = new List<Ellipse> {C0,C1,C2,C3,C4,C5,C6,C7,C8};
           
            animationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle, Dispatcher) {Interval = new TimeSpan(0, 0, 0, 0, 75)};
        }

        
        public bool IsSpinning
        {
            get { return (bool)GetValue(IsSpinningProperty); }
            set { SetValue(IsSpinningProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSpinning.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSpinningProperty =
            DependencyProperty.Register("IsSpinning", typeof(bool), typeof(LoadingWait), new PropertyMetadata(true, OnIsSpinningChanged));

        private static void OnIsSpinningChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var me = (LoadingWait)dependencyObject;
            if((bool)args.NewValue && !me.animationTimer.IsEnabled)
                me.Start();
            else if (!(bool)args.NewValue && me.animationTimer.IsEnabled)
                me.Stop();
        }


        private void Start()
        {
            animationTimer.Tick += HandleAnimationTick;
            animationTimer.Start();
        }

        private void Stop()
        {
            animationTimer.Stop();
            animationTimer.Tick -= HandleAnimationTick;
        }

        private void HandleAnimationTick(object sender, EventArgs e)
        {
            //Foreground = Brushes.Violet;
            if(IsSpinning)
                SpinnerRotate.Angle = (SpinnerRotate.Angle + 36) % 360;
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            const double offset = Math.PI;
            const double step = Math.PI * 2 / 10.0;
            foreach (Ellipse t in ellipses)
                SetPosition(t, offset, Convert.ToDouble(ellipses.IndexOf(t)), step);
        }

        private void SetPosition(Ellipse ellipse, double offset,
            double posOffSet, double step)
        {
            ellipse.SetValue(Canvas.LeftProperty, 50.0 + Math.Sin(offset + posOffSet * step) * 50.0);
            ellipse.SetValue(Canvas.TopProperty, 50 + Math.Cos(offset + posOffSet * step) * 50.0);
        }

        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void HandleVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                Start();
            else
                Stop();
        }
    }
}
