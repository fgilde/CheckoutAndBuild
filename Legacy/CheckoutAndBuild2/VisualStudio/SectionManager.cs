using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	public static class SectionManager
	{
		static readonly Dictionary<ITeamExplorerSection, PropertyChangedEventHandler> includedChangeHandlers = new Dictionary<ITeamExplorerSection, PropertyChangedEventHandler>();

		internal static void UpdateSectionIncluded(ITeamExplorerPage teamExplorerPage)
		{
		    var pageAttribute = teamExplorerPage?.GetType().GetAttributes<TeamExplorerPageAttribute>(true).FirstOrDefault();
		    if (!string.IsNullOrEmpty(pageAttribute?.Id))
		    {
		        var pageId = pageAttribute.Id;
		        foreach (var section in teamExplorerPage.GetSections())
		        {
		            if (section != null)
		            {
		                var pageInfo = new PageInfo(pageId, 0, section.GetType().Name);
		                SetSectionIsIncluded(section, pageInfo.IsIncluded);
		            }
		        }
		    }
		}

		internal static  void SetSectionIsIncluded(ITeamExplorerSection section, bool isIncluded)
		{
			if (isIncluded)
				IncludeSection(section);
			else
				ExcludeSection(section);
		}
		
		internal static void IncludeSection(ITeamExplorerSection section)
		{
			if (!section.IsVisible)
			{
				var teamExplorerBase = section as TeamExplorerBase;
				if (teamExplorerBase != null)
					teamExplorerBase.IsEnabled = true;
			    var handler = GetPropertyChangedHandler(section);
			    if (handler != null)
			    {
			        section.PropertyChanged -= handler;
			    }
			    section.IsVisible = true;
			}
		}

		internal static void ExcludeSection(ITeamExplorerSection section)
		{
			var teamExplorerBase = section as TeamExplorerBase;
			if (teamExplorerBase != null)
				teamExplorerBase.IsEnabled = false;
		    var handler = GetPropertyChangedHandler(section);
		    if (handler != null)
		    {
		        section.PropertyChanged += handler;
		    }
		    section.IsVisible = false;			
		}
		
		private static PropertyChangedEventHandler GetPropertyChangedHandler(ITeamExplorerSection section)
		{
			if (!includedChangeHandlers.ContainsKey(section))
			{
				includedChangeHandlers.Add(section, (sender, args) =>
				{
					if (args.PropertyName == "IsVisible")
					{
						//SettingsService settingsService = serviceProvider.Get<SettingsService>();
						if (section.IsVisible)
							section.IsVisible = false;
					}
				});
			}
			return includedChangeHandlers[section];
		}

	}
}