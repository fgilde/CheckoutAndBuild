using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildSettingsSection, TeamExplorerPageIds.Settings, 1000)]
	[TeamExplorerSectionPlacement(GuidList.checkoutAndBuildTeamExplorerMainPage, 100)]
	public class CheckoutAndBuildSettingsSection : TeamExplorerBase
	{
		public override object Content
		{
			get
			{
				return new ItemsControl {ItemsSource = Links, Margin = new Thickness(10,10,0,0)};
			}
		}

		public IEnumerable<TextLink> Links
		{
			get
			{
				yield return StaticCommands.SettingsCommand.ToTextLink();
				yield return StaticCommands.SectionSettingsCommand.ToTextLink();
				yield return StaticCommands.ResetSettingsCommand.ToTextLink();
			}
		}

		public override string Title
		{
			get { return string.Format("{0} - Settings", Const.ApplicationName); }
		}

	}
}