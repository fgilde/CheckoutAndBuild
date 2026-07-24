using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Controls;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class QueryEditor : UserControl
	{
		#region Privates

		private WorkItemStore store;

		private List<Keyword> wordList = new List<Keyword>()
		{
			new Keyword("SELECT", Color.Blue),
			new Keyword("FROM", Color.Blue),
			new Keyword("WHERE", Color.Blue),
			new Keyword("LIKE", Color.Blue),
		};

		private QueryEditMode editMode;

		#endregion

		public QueryEditor()
		{
			InitializeComponent();
			RecreateControl();
		}

		public TfsContext TfsContext => CheckoutAndBuild2Package.GetGlobalService<TfsContext>();

	    public QueryBuilderControl QueryBuilderControl { get; private set; }

		public QueryDefinition SelectedQuery { get; set; }

		public bool CanRunQuery
		{
			get { return toolStripButtonRun.Visible; }
			set { toolStripButtonRun.Visible = value; }
		}

		public QueryEditMode EditMode
		{
			get { return editMode; }
			set
			{
				editMode = value;
				UpdateMode();
			}
		}


		public event EventHandler RunQuery;

		public void DoRunQuery()
		{
			EventHandler handler = RunQuery;
			if (handler != null) handler(this, EventArgs.Empty);
		}


		public WorkItemStore Store
		{
			get
			{
				if (!TfsContext.IsTfsConnected)
					return null;
				return store ?? (store = new WorkItemStore(TfsContext.TeamProjectCollection));
			}
			set { store = value; RecreateControl(); }
		}

		public string Query
		{
			get
			{
				if (QueryBuilderControl == null)
					return string.Empty;
				return GetWigl(QueryBuilderControl.GenerateWiql(new ResultOptions()));
			}
			set
			{
				try
				{
					if (QueryBuilderControl != null)
						QueryBuilderControl.SetWiql(GetWigl(value));
					UpdateResultLabel();
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}
		}

		public void OpenQuery()
		{
			var res = TeamControlFactory.ShowDialogQueryPicker(TfsContext.GetTeamProjects(), SelectedQuery, TeamControlFactory.QueryPickerType.PickQuery) as QueryDefinition;
			if (res != null)
			{
				SelectedQuery = res;
				Query = res.QueryText;
				if (CanRunQuery)
					DoRunQuery();
			}
		}

		public void SaveQueryAs()
		{
			if (SelectedQuery != null)
			{
				TeamControlFactory.ShowDialogSaveQueryAs(SelectedQuery);
			}
			else
			{
				var folder = TfsContext.GetTeamProject().QueryHierarchy.OfType<QueryFolder>().FirstOrDefault() ??
							 TeamControlFactory.ShowDialogQueryPicker(TfsContext.GetTeamProjects(), null, TeamControlFactory.QueryPickerType.PickFolder) as QueryFolder;
				if (folder != null)
				{
					TeamControlFactory.ShowDialogSaveQueryAs(folder, Query, "New Query");
				}
			}
		}

		#region Private Methods

		private string GetWigl(string queryWigl)
		{
			if (!queryWigl.ToLower().StartsWith("select"))
				queryWigl = "SELECT *" + queryWigl;
			return queryWigl;
		}


		private void RecreateControl()
		{
			UpdateProjectNames();
			if (TfsContext.IsTfsConnected)
			{
				panelBuilder.Controls.Clear();
				QueryBuilderControl = new QueryBuilderControl(Store, TfsContext.ConfigurationServer.InstanceId) {Dock = DockStyle.Fill};
				QueryBuilderControl.FilterExpressionChanged += QueryBuilderControlOnFilterExpressionChanged;
				panelBuilder.Controls.Add(QueryBuilderControl);
			}
		}

		private void QueryBuilderControlOnFilterExpressionChanged(object sender, EventArgs eventArgs)
		{
			UpdateResultLabel();
		}

		private CancellationTokenSource source;
		private void UpdateResultLabel()
		{
			resultLabel.Text = "";
			//resultLabel.Text = "(Loading...)";
			//var workItemCollection = await GetQueryResults();
			//resultLabel.Text = string.Format("{0} Results", workItemCollection != null ? workItemCollection.Count : 0);
		}

		private async Task<WorkItemCollection> GetQueryResults()
		{
			if(source != null && !source.IsCancellationRequested)
				source.Cancel();
			source = new CancellationTokenSource();
			return await Check.TryCatchAsync<WorkItemCollection, Exception>(Task.Run(() => store.Query(WorkItemManager.PrepareQueryText(Query)), source.Token));
		}

		private void UpdateProjectNames()
		{
			if (!TfsContext.IsTfsConnected)
				comboProjectNames.Items.Clear();
			if (comboProjectNames.Items.Count == 0)
			{
				if (TfsContext.IsTfsConnected)
				{

					foreach (var project in new WorkItemStore(TfsContext.TeamProjectCollection).Projects.OfType<Project>().OrderBy(project => project.Name))
					{
						comboProjectNames.Items.Add(project.Name);
					}
				}
				if (comboProjectNames.Items.Count > 0)
					comboProjectNames.SelectedIndex = 0;
			}
		}

		private void toolStripButtonRun_Click(object sender, EventArgs e)
		{
			DoRunQuery();
		}

		private void toolStripButtonToggleMode_Click(object sender, EventArgs e)
		{
			ToggleEditMode();
		}

		private void UpdateMode()
		{
			toolStripButtonToggleMode.Checked = EditMode == QueryEditMode.QueryBuilder;
			panelBuilder.Visible = toolStripButtonToggleMode.Checked;
			panelTextEdit.Visible = !toolStripButtonToggleMode.Checked;
			if (panelTextEdit.Visible)
			{
				queryTextBox.Text = Query;
			}
			else
			{
				Query = (queryTextBox.Text);
			}
		}

		public void ToggleEditMode()
		{
			EditMode = EditMode == QueryEditMode.QueryBuilder ? QueryEditMode.TextEdit : QueryEditMode.QueryBuilder;
		}

		private void toolStripButtonSaveQuery_Click(object sender, EventArgs e)
		{
			SaveQueryAs();
		}


		private void queryTextBox_TextChanged(object sender, EventArgs e)
		{
			Keyword.ColorizeKeywords(queryTextBox, wordList);
			if (EditMode == QueryEditMode.TextEdit)
			{
				string wqlq = Query;
				try
				{
					Query = (queryTextBox.Text);
				}
				catch (Exception)
				{
					Query = wqlq;
				}
			}
		}

		private void comboProjectNames_SelectedIndexChanged(object sender, EventArgs e)
		{
			RecreateControl();
		}

		private void toolStripButtonOpenQuery_Click(object sender, EventArgs e)
		{
			OpenQuery();
		}

		#endregion
		
	}

	public enum QueryEditMode
	{
		QueryBuilder,
		TextEdit
	}
}
