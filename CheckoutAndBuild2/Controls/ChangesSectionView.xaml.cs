using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for ChangesSectionView.xaml
    /// </summary>
    public partial class ChangesSectionView
    {
        public ChangesSectionView()
        {
            InitializeComponent();			
        }

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

        /// <summary>
        /// Parent section.
        /// </summary>
        public ChangesSectionBase ParentSection
        {
            get { return (ChangesSectionBase)GetValue(ParentSectionProperty); }
            set { SetValue(ParentSectionProperty, value); }
        }
        public static readonly DependencyProperty ParentSectionProperty =
            DependencyProperty.Register("ParentSection", typeof(ChangesSectionBase), typeof(ChangesSectionView));

        /// <summary>
        /// View changeset details.
        /// </summary>
        private void ViewChangesetDetails()
        {
            if (changesetList.SelectedItems.Count == 1)
            {
                var changeset = changesetList.SelectedItems[0] as ChangesetInfo;

                if (changeset != null)
                    ParentSection.ViewChangesetDetails(changeset.Changeset.ChangesetId);
            }
        }

        /// <summary>
        /// Changeset list MouseDoubleClick event handler.
        /// </summary>
        private void changesetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && changesetList.SelectedItems.Count == 1)
            {
                ViewChangesetDetails();
            }
        }

        /// <summary>
        /// Changeset list KeyDown event handler.
        /// </summary>
        private void changesetList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && changesetList.SelectedItems.Count == 1)
            {
                ViewChangesetDetails();
            }
        }

        /// <summary>
        /// History link Click event handler.
        /// </summary>
        private void historyLink_Click(object sender, RoutedEventArgs e)
        {
            ParentSection.ViewHistory();
        }

        /// <summary>
        /// Get/set the selected index.
        /// </summary>
        public int SelectedIndex
        {
            get { return changesetList.SelectedIndex; }
            set { changesetList.SelectedIndex = value; changesetList.ScrollIntoView(changesetList.SelectedItem); }
        }

	    private void findChangeset(object sender, RoutedEventArgs e)
	    {
			VersionControlExt vce = CheckoutAndBuild2Package.GetGlobalService<VersionControlExt>();
			if (vce != null)
			{
				vce.FindChangeset();
			}
	    }

	    private void viewAllRecentChanges(object sender, RoutedEventArgs e)
	    {
			CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>().NavigateToPage(GuidList.recentChangesPage.ToGuid(), null);	
	    }

	    private void ChangesetList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	    {
		    var selectedItem = changesetList.SelectedItem as ChangesetInfo;
			if(selectedItem != null)
				VisualStudioTrackingSelection.UpdateSelectionTracking(selectedItem.Changeset);
	    }
    }

    #region Converters

    /// <summary>
    /// Changeset comment converter class.
    /// </summary>
    public class ChangesetCommentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string comment = (value is string) ? (string)value : String.Empty;
            StringBuilder sb = new StringBuilder(comment);
            sb.Replace('\r', ' ');
            sb.Replace('\n', ' ');
            sb.Replace('\t', ' ');

            if (sb.Length > 64)
            {
                sb.Remove(61, sb.Length - 61);
                sb.Append("...");
            }

            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    #endregion
}
