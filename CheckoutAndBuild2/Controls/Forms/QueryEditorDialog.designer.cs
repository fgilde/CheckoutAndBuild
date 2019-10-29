namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class QueryEditorDialog
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(QueryEditorDialog));
			this.panel1 = new System.Windows.Forms.Panel();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.buttonOk = new System.Windows.Forms.Button();
			this.splitContainer = new System.Windows.Forms.SplitContainer();
			this.queryEditor1 = new QueryEditor();
			this.panelMain = new System.Windows.Forms.Panel();
			this.panelSelectQuery = new System.Windows.Forms.Panel();
			this.splitter1 = new System.Windows.Forms.Splitter();
			this.panelSelectQuery = new System.Windows.Forms.Panel();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
			this.splitContainer.Panel1.SuspendLayout();
			this.splitContainer.Panel2.SuspendLayout();
			this.splitContainer.SuspendLayout();
			this.panelMain.SuspendLayout();
			this.SuspendLayout();
			// 
			// panel1
			// 
			this.panel1.BackColor = System.Drawing.SystemColors.Control;
			this.panel1.Controls.Add(this.buttonCancel);
			this.panel1.Controls.Add(this.buttonOk);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panel1.Location = new System.Drawing.Point(0, 358);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(628, 45);
			this.panel1.TabIndex = 7;
			// 
			// buttonCancel
			// 
			this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.buttonCancel.Location = new System.Drawing.Point(539, 10);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 23);
			this.buttonCancel.TabIndex = 1;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			// 
			// buttonOk
			// 
			this.buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.buttonOk.Location = new System.Drawing.Point(449, 10);
			this.buttonOk.Name = "buttonOk";
			this.buttonOk.Size = new System.Drawing.Size(75, 23);
			this.buttonOk.TabIndex = 0;
			this.buttonOk.Text = "OK";
			this.buttonOk.UseVisualStyleBackColor = true;
			// 
			// splitContainer
			// 
			this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer.Location = new System.Drawing.Point(0, 0);
			this.splitContainer.Name = "splitContainer";
			this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer.Panel1
			// 
			this.splitContainer.Panel1.Controls.Add(this.queryEditor1);
			// 
			// splitContainer.Panel2
			// 	
			this.splitContainer.Size = new System.Drawing.Size(428, 358);
			this.splitContainer.SplitterDistance = 42;
			this.splitContainer.TabIndex = 10;
			// 
			// queryEditor1
			// 
			this.queryEditor1.CanRunQuery = true;
			this.queryEditor1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.queryEditor1.EditMode = QueryEditMode.QueryBuilder;
			this.queryEditor1.Location = new System.Drawing.Point(0, 0);
			this.queryEditor1.Name = "queryEditor1";
			this.queryEditor1.Query = "";
			this.queryEditor1.SelectedQuery = null;
			this.queryEditor1.Size = new System.Drawing.Size(428, 42);
			this.queryEditor1.Store = null;
			this.queryEditor1.TabIndex = 9;
			// 
			// panelMain
			// 
			this.panelMain.Controls.Add(this.splitter1);
			this.panelMain.Controls.Add(this.splitContainer);
			this.panelMain.Controls.Add(this.panelSelectQuery);
			this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelMain.Location = new System.Drawing.Point(0, 0);
			this.panelMain.Name = "panelMain";
			this.panelMain.Size = new System.Drawing.Size(628, 358);
			this.panelMain.TabIndex = 11;
			// 
			// panelSelectQuery
			// 
			this.panelSelectQuery.Dock = System.Windows.Forms.DockStyle.Right;
			this.panelSelectQuery.Location = new System.Drawing.Point(428, 0);
			this.panelSelectQuery.Name = "panelSelectQuery";
			this.panelSelectQuery.Size = new System.Drawing.Size(200, 358);
			this.panelSelectQuery.TabIndex = 11;
			this.panelSelectQuery.Visible = false;
			// 
			// splitter1
			// 
			this.splitter1.Dock = System.Windows.Forms.DockStyle.Right;
			this.splitter1.Location = new System.Drawing.Point(425, 0);
			this.splitter1.Name = "splitter1";
			this.splitter1.Size = new System.Drawing.Size(3, 358);
			this.splitter1.TabIndex = 12;
			this.splitter1.TabStop = false;
			this.splitter1.Visible = false;
			// 
			// panelSelectQuery
			// 
			this.panelSelectQuery.Dock = System.Windows.Forms.DockStyle.Right;
			this.panelSelectQuery.Location = new System.Drawing.Point(428, 0);
			this.panelSelectQuery.Name = "panelSelectQuery";
			this.panelSelectQuery.Size = new System.Drawing.Size(200, 358);
			this.panelSelectQuery.TabIndex = 11;
			this.panelSelectQuery.Visible = false;
			// 
			// QueryEditorDialog
			// 
			this.AcceptButton = this.buttonOk;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.buttonCancel;
			this.ClientSize = new System.Drawing.Size(628, 403);
			this.Controls.Add(this.panelMain);
			this.Controls.Add(this.panel1);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "QueryEditorDialog";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Query Editor";
			this.panel1.ResumeLayout(false);
			this.splitContainer.Panel1.ResumeLayout(false);
			this.splitContainer.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
			this.splitContainer.ResumeLayout(false);
			this.panelMain.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonOk;
		private System.Windows.Forms.SplitContainer splitContainer;
		private QueryEditor queryEditor1;
		
		private System.Windows.Forms.Panel panelMain;
		private System.Windows.Forms.Panel panelSelectQuery;
		private System.Windows.Forms.Splitter splitter1;
	}
}