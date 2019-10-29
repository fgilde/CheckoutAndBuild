using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for Sophos.xaml
	/// </summary>
	public partial class Sophos
	{		
		public static bool Ignored { get; set; }

		public Sophos()
		{
			InitializeComponent();
		}


		private void Button_OnClick(object sender, RoutedEventArgs e)
		{
            
			Ignored = true;
			var span = TimeSpan.FromSeconds(1);
			label.BeginAnimation(OpacityProperty, new DoubleAnimation(1, new Duration(span)));
			button.BeginAnimation(OpacityProperty, new DoubleAnimation(0, new Duration(span)));
			Task.Delay(700).ContinueWith(task =>
			{								
				CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>().NavigateToPage(new Guid(TeamExplorerPageIds.Home), null);
			}, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
		}
	}
}
