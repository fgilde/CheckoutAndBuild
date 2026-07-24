using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class OptionsSectionPageControl : UserControl
	{		
		private static Type[] coabSectionTypes;
		private static Type[] allSectionTypes;
	    private bool initStateSectionManagementEnabled;

        private Type SelectedType => listBoxSection.SelectedItem as Type;

	    internal CheckoutAndBuildSectionOptionsPage OptionsPage { get; set; }

		public OptionsSectionPageControl()
		{			
			InitializeComponent();			
			listViewIncludedPages.ItemCheck += ListViewIncludedPagesOnItemCheck;
		}


	    private bool SetUIEnabled(bool enabled)
	    {
	        checkBoxManageAllSection.Enabled = listBoxSection.Enabled = listViewIncludedPages.Enabled = enabled;
	        return enabled;
	    }

		#region Typeload Behavior

		private async Task<Type[]> GetTypesAsync(bool loadAllSections)
		{			
			return await Task.Run(() =>
			{
				if (loadAllSections)
				{
					if (allSectionTypes == null)
					{
						Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
						SetProgress(0, assemblies.Length);
						var list = new List<Type>();
						foreach (Assembly a in assemblies)
						{
							SetProgress();
							var a1 = a;
							Check.TryCatch<Exception>(() => list.AddRange(a1.GetTypes().Where(type => type.GetCustomAttribute<TeamExplorerSectionAttribute>() != null)));
						}						
						allSectionTypes = list.ToArray();
						list.Clear();
					}
					return allSectionTypes;
				}
				var res = coabSectionTypes ?? (coabSectionTypes = typeof(TeamExplorerBase).Assembly
					.GetTypes()
					.Where(type => type.GetCustomAttribute<TeamExplorerSectionAttribute>() != null)
					.ToArray());
				return res;
			});
		}

		void SetProgress(int value = -1, int max = -1)
		{
			Invoke(new Action(() =>
			{
				progressBar.Style = ProgressBarStyle.Blocks;
				if (max != -1)
					progressBar.Maximum = max;
				progressBar.Value = value != -1 ? value : (++progressBar.Value);
			}));
		}

		void SetLoading(bool isLoading)
		{
			if(isLoading)
				progressBar.Style = ProgressBarStyle.Marquee;
			Enabled = !isLoading;
			panelLoading.Visible = isLoading && (allSectionTypes == null || coabSectionTypes == null);
		}

		private async Task UpdateSectionTypesAsync(bool loadAllSections = false)
		{
			try
			{
				SetLoading(true);
				listViewIncludedPages.Items.Clear();
				listBoxSection.Items.Clear();
				listBoxSection.BeginUpdate();
				var types = await GetTypesAsync(loadAllSections);
				listBoxSection.Items.AddRange(types);
			}
			finally
			{
				listBoxSection.EndUpdate();
				SetLoading(false);
			}
		}

		#endregion
		
		private async void checkBoxManageAllSection_CheckedChanged(object sender, EventArgs e)
		{
			await UpdateSectionTypesAsync(checkBoxManageAllSection.Checked);	
		}

		private void ListViewIncludedPagesOnItemCheck(object sender, ItemCheckEventArgs e)
		{
			var info = listViewIncludedPages.Items[e.Index].Tag as PageInfo;
			if (info != null)
				info.IsIncluded = e.NewValue == CheckState.Checked;
		}

		internal async void Initialize()
		{		    
            checkBoxSectionManagementEnabled.Checked = initStateSectionManagementEnabled = SetUIEnabled(CheckoutAndBuild2Package.GetGlobalService<SettingsService>().Get(SettingsKeys.EnableSectionManagement, false));
            await UpdateSectionTypesAsync(checkBoxManageAllSection.Checked);	
		}

		private void AddItem(PageInfo info)
		{			
			listViewIncludedPages.Items.Add(new ListViewItem { Checked = info.IsIncluded, Tag = info, Text = info.PageName });
		}

		private void listBoxSection_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (SelectedType != null)
			{
				listViewIncludedPages.Items.Clear();
				listViewIncludedPages.BeginUpdate();
				SelectedType.GetCustomAttributes<TeamExplorerSectionAttribute>().Apply(attribute => AddItem(new PageInfo(attribute.ParentPageId, attribute.Priority, SelectedType.Name)));
				SelectedType.GetCustomAttributes<TeamExplorerSectionPlacementAttribute>().Apply(attribute => AddItem(new PageInfo(attribute.PlacementParentPageId, attribute.PlacementPriority, SelectedType.Name)));
				listViewIncludedPages.EndUpdate();
			}
		}

        private void checkBoxSectionManagementEnabled_CheckedChanged(object sender, EventArgs e)
        {
            CheckoutAndBuild2Package.GetGlobalService<SettingsService>().Set(SettingsKeys.EnableSectionManagement, checkBoxSectionManagementEnabled.Checked);
            SetUIEnabled(checkBoxSectionManagementEnabled.Checked);
            panelNotification.Visible = checkBoxSectionManagementEnabled.Checked != initStateSectionManagementEnabled;
        }
    }


    internal class PageInfo
	{
		private readonly string sectionTypeName;
		static readonly FieldInfo[] memberInfosVS = typeof(TeamExplorerPageIds).GetFields();
		static readonly FieldInfo[] memberInfosCOAB = typeof(GuidList).GetFields();
		SettingsService settingsService => CheckoutAndBuild2Package.GetGlobalService<SettingsService>();

	    public int Priority { get; private set; }
		public string PageName { get; private set; }
		public string PageId { get; private set; }
	
		public bool IsIncluded
		{
			get { return settingsService?.Get(SettingsKey, true) ?? true; }
		    set { settingsService?.Set(SettingsKey, value); }
		}

		public SettingsKey SettingsKey => new SettingsKey($"section{sectionTypeName}_in_page_{PageId}_Included", true);

        /// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public PageInfo(string pageId, int priority, string sectionTypeName)
		{
			this.sectionTypeName = sectionTypeName;
			Priority = priority;
			PageName = PageId = pageId;
			PageName = ResolveName(pageId);
		}

		private string ResolveName(string pageId)
		{
			var pageInfo = memberInfosVS.FirstOrDefault(info => info.GetValue(null).ToString() == pageId) ??
							memberInfosCOAB.FirstOrDefault(info => info.GetValue(null).ToString() == pageId);
			if (pageInfo != null)
			{
				string scope = pageInfo.DeclaringType == typeof (GuidList) ? "COAB" : "VS";
				return $"{pageInfo.Name.ToUpper(true)} ({scope})";			
			}
			return pageId;
		}
	}
}