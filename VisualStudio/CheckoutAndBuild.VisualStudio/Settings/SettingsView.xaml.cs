using System.Windows.Controls;

namespace CheckoutAndBuild.VisualStudio.Settings
{
	/// <summary>Dynamic settings editor; DataContext is a <see cref="SettingsViewModel"/>.</summary>
	public partial class SettingsView : UserControl
	{
		public SettingsView()
		{
			InitializeComponent();
		}
	}
}
