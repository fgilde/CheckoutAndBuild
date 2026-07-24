using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for DictionaryEdit.xaml
	/// </summary>
	public partial class StringListEdit : INotifyPropertyChanged
	{
	    private BrowseMode browseMode;

	    public BrowseMode BrowseMode
	    {
	        get { return browseMode; }
	        set
	        {
	            browseMode = value;
	            RaisePropertyChanged();
	            RaisePropertyChanged("HasBrowsMode");
	        }
	    }

	    public bool HasBrowsMode => BrowseMode != BrowseMode.None;

		public StringListEdit()
		{
            BrowseMode = BrowseMode.None;
            InitializeComponent();
		}

	    public event PropertyChangedEventHandler PropertyChanged;

	    protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
	    {
	        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	    }

	    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
	    {
	        if (BrowseMode != BrowseMode.None)
	        {
	            if (BrowseMode == BrowseMode.Directories)
	            {
	                var dlg = new FolderBrowserDialog();
	                if (dlg.ShowDialog() == DialogResult.OK)
	                    SetItemValue(dlg.SelectedPath);
	            }
	            else
	            {
	                var dlg = new OpenFileDialog
	                {
	                    CheckFileExists = BrowseMode == BrowseMode.Files,
                        CheckPathExists = false,
	                    ValidateNames = BrowseMode == BrowseMode.Files
	                };
	                if (dlg.ShowDialog() == DialogResult.OK)
                        SetItemValue(dlg.FileName);
                }
	        }
	    }

	    private void SetItemValue(string value)
	    {
            var item = innerGrid.SelectedItem as Bindable<string>;
	        if (item != null)
	            item.Value = value;
	    }
	}

    public enum BrowseMode
    {
        None,
        Directories,
        Files,
        DirectoriesAndFiles
    }
}
