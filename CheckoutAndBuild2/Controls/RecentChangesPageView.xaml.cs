﻿using System;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;

namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for RecentChangesPageView.xaml
    /// </summary>
    public partial class RecentChangesPageView
    {
        public RecentChangesPageView()
        {
            InitializeComponent();			
        }

	    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}
    }
}
