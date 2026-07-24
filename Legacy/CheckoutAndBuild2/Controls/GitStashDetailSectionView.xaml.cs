using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio.Sections;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for UserPendingChangesView.xaml
	/// </summary>
	public partial class GitStashDetailSectionView
    {
		internal GitStashDetailSection Section => DataContext as GitStashDetailSection;

        public GitStashDetailSectionView()
		{
			InitializeComponent();
		}


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

        private void changeList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            
        }
    }
}
