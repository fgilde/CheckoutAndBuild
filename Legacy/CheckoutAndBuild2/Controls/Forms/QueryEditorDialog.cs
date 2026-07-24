using System;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class QueryEditorDialog : Form
	{
		public dynamic QueryPickerTree { get; private set; }

		public bool CanRunQuery
		{
			get { return queryEditor1.CanRunQuery; }
			set { queryEditor1.CanRunQuery = value; }
		}

		public WorkItemStore Store
		{
			get { return queryEditor1.Store; }
			set { queryEditor1.Store = value; }
		}

		public bool CanSelectQuery
		{
			get { return panelSelectQuery.Visible; }
			set
			{
				panelSelectQuery.Visible = value;
				splitter1.Visible = value;
			}
		}

		public QueryDefinition SelectedQuery
		{
			get { return queryEditor1.SelectedQuery; }
			set { queryEditor1.SelectedQuery = value; }
		}

		public string Query
		{
			get { return queryEditor1.Query; }
			set { queryEditor1.Query = value; }
		}

		public event EventHandler RunQuery
		{
			add { queryEditor1.RunQuery += value; }
			remove { queryEditor1.RunQuery -= value; }
		}

		public QueryEditorDialog(QueryDefinition query)
			: this(query.QueryText)
		{
			SelectedQuery = query;
		}

		public QueryEditorDialog()
			: this(string.Empty)
		{ }

		public QueryEditorDialog(string query)
		{
			InitializeComponent();
			splitContainer.Panel2.Visible = false;
			splitContainer.SplitterDistance = Height;
			if (!string.IsNullOrEmpty(query))
				Query = query;
			Shown += OnShown;
			InitQueryPicker();
		}

		private void InitQueryPicker(QueryItem itemToSelect = null)
		{			
			panelSelectQuery.Controls.Clear();
			QueryPickerTree = TeamControlFactory.CreateQueryPickerTreeControl(queryEditor1.TfsContext.GetTeamProjects(), null, TeamControlFactory.QueryPickerType.PickQuery);
			var ctro = (UserControl)QueryPickerTree;
			ctro.Dock = DockStyle.Fill;
			panelSelectQuery.Controls.Add(ctro);
			var tree = QueryPickerTree.m_queryTree as TreeView;
			tree.AfterSelect -= SelectedItemPropertyChanged;
			tree.AfterSelect += SelectedItemPropertyChanged;
			if (itemToSelect != null)
				QueryPickerTree.SelectedItem = itemToSelect;
		}

		private void SelectedItemPropertyChanged(object sender, TreeViewEventArgs e)
		{
			if (QueryPickerTree.SelectedItem != null)
			{
				if (((QueryPickerTree.SelectedItem is QueryDefinition)))
				{
					queryEditor1.SelectedQuery = QueryPickerTree.SelectedItem;
					queryEditor1.Query = queryEditor1.SelectedQuery.QueryText;
				}
			}
		}

		private void OnShown(object s, EventArgs eventArgs)
		{
			buttonOk.Visible = Modal;
			if (!Modal)
			{
				buttonCancel.Text = "Close";
				buttonCancel.Click += (sender, args) => Close();
			}
		}

		public void ShowQueryResults()
		{
			if (!splitContainer.Panel2.Visible)
			{
				Height += 300;
				splitContainer.SplitterDistance = 100;
				splitContainer.Panel2.Visible = true;
			}
			MessageBox.Show("TODO:");
		}

		public WorkItemCollection GetWorkItemCollection()
		{
			return Store.Query(Query);
		}
	}
}
