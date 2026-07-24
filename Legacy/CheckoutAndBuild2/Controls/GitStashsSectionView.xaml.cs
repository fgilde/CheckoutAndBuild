using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio.Sections;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for UserPendingChangesView.xaml
	/// </summary>
	public partial class GitStashsSectionView
    {
		internal GitStashsSection Section => DataContext as GitStashsSection;

        public GitStashsSectionView()
		{
			InitializeComponent();
		}


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

        private void StashList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Section?.SelectedStash != null)
                Section.ShowStashDetailsCommand.Execute(null);
        }
    }
}
