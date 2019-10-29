namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class QueryEditor
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(QueryEditor));
			this.toolStrip1 = new System.Windows.Forms.ToolStrip();
			this.toolStripButtonRun = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.toolStripButtonToggleMode = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
			this.toolStripButtonOpenQuery = new System.Windows.Forms.ToolStripButton();
			this.toolStripButtonSaveQuery = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
			this.comboProjectNames = new System.Windows.Forms.ToolStripComboBox();
			this.panelBuilder = new System.Windows.Forms.Panel();
			this.panelTextEdit = new System.Windows.Forms.Panel();
			this.queryTextBox = new System.Windows.Forms.RichTextBox();
			this.resultLabel = new System.Windows.Forms.LinkLabel();
			this.toolStrip1.SuspendLayout();
			this.panelTextEdit.SuspendLayout();
			this.SuspendLayout();
			// 
			// toolStrip1
			// 
			this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonRun,
            this.toolStripSeparator1,
            this.toolStripButtonToggleMode,
            this.toolStripSeparator2,
            this.toolStripButtonOpenQuery,
            this.toolStripButtonSaveQuery,
            this.toolStripSeparator3,
            this.comboProjectNames});
			this.toolStrip1.Location = new System.Drawing.Point(0, 0);
			this.toolStrip1.Name = "toolStrip1";
			this.toolStrip1.Size = new System.Drawing.Size(455, 25);
			this.toolStrip1.TabIndex = 0;
			this.toolStrip1.Text = "toolStrip1";
			// 
			// toolStripButtonRun
			// 
			this.toolStripButtonRun.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripButtonRun.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonRun.Image")));
			this.toolStripButtonRun.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripButtonRun.Name = "toolStripButtonRun";
			this.toolStripButtonRun.Size = new System.Drawing.Size(23, 22);
			this.toolStripButtonRun.Text = "Execute Query";
			this.toolStripButtonRun.Click += new System.EventHandler(this.toolStripButtonRun_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
			// 
			// toolStripButtonToggleMode
			// 
			this.toolStripButtonToggleMode.Checked = true;
			this.toolStripButtonToggleMode.CheckState = System.Windows.Forms.CheckState.Checked;
			this.toolStripButtonToggleMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripButtonToggleMode.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonToggleMode.Image")));
			this.toolStripButtonToggleMode.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripButtonToggleMode.Name = "toolStripButtonToggleMode";
			this.toolStripButtonToggleMode.Size = new System.Drawing.Size(23, 22);
			this.toolStripButtonToggleMode.Text = "Toggle Textmode";
			this.toolStripButtonToggleMode.Click += new System.EventHandler(this.toolStripButtonToggleMode_Click);
			// 
			// toolStripSeparator2
			// 
			this.toolStripSeparator2.Name = "toolStripSeparator2";
			this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
			// 
			// toolStripButtonOpenQuery
			// 
			this.toolStripButtonOpenQuery.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripButtonOpenQuery.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonOpenQuery.Image")));
			this.toolStripButtonOpenQuery.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripButtonOpenQuery.Name = "toolStripButtonOpenQuery";
			this.toolStripButtonOpenQuery.Size = new System.Drawing.Size(23, 22);
			this.toolStripButtonOpenQuery.Text = "Open Query";
			this.toolStripButtonOpenQuery.Click += new System.EventHandler(this.toolStripButtonOpenQuery_Click);
			// 
			// toolStripButtonSaveQuery
			// 
			this.toolStripButtonSaveQuery.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripButtonSaveQuery.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonSaveQuery.Image")));
			this.toolStripButtonSaveQuery.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripButtonSaveQuery.Name = "toolStripButtonSaveQuery";
			this.toolStripButtonSaveQuery.Size = new System.Drawing.Size(23, 22);
			this.toolStripButtonSaveQuery.Text = "Save as";
			this.toolStripButtonSaveQuery.Click += new System.EventHandler(this.toolStripButtonSaveQuery_Click);
			// 
			// toolStripSeparator3
			// 
			this.toolStripSeparator3.Name = "toolStripSeparator3";
			this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
			// 
			// comboProjectNames
			// 
			this.comboProjectNames.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboProjectNames.Name = "comboProjectNames";
			this.comboProjectNames.Size = new System.Drawing.Size(121, 25);
			this.comboProjectNames.SelectedIndexChanged += new System.EventHandler(this.comboProjectNames_SelectedIndexChanged);
			// 
			// panelBuilder
			// 
			this.panelBuilder.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelBuilder.Location = new System.Drawing.Point(0, 25);
			this.panelBuilder.Name = "panelBuilder";
			this.panelBuilder.Size = new System.Drawing.Size(455, 117);
			this.panelBuilder.TabIndex = 1;
			// 
			// panelTextEdit
			// 
			this.panelTextEdit.Controls.Add(this.queryTextBox);
			this.panelTextEdit.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelTextEdit.Location = new System.Drawing.Point(0, 25);
			this.panelTextEdit.Name = "panelTextEdit";
			this.panelTextEdit.Size = new System.Drawing.Size(455, 117);
			this.panelTextEdit.TabIndex = 0;
			this.panelTextEdit.Visible = false;
			// 
			// queryTextBox
			// 
			this.queryTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.queryTextBox.Location = new System.Drawing.Point(0, 0);
			this.queryTextBox.Name = "queryTextBox";
			this.queryTextBox.Size = new System.Drawing.Size(455, 117);
			this.queryTextBox.TabIndex = 0;
			this.queryTextBox.Text = "";
			this.queryTextBox.TextChanged += new System.EventHandler(this.queryTextBox_TextChanged);
			// 
			// resultLabel
			// 
			this.resultLabel.AutoSize = true;
			this.resultLabel.Location = new System.Drawing.Point(262, 6);
			this.resultLabel.Name = "resultLabel";
			this.resultLabel.Size = new System.Drawing.Size(57, 13);
			this.resultLabel.TabIndex = 2;
			this.resultLabel.TabStop = true;
			this.resultLabel.Text = "(Loading..)";
			// 
			// QueryEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.resultLabel);
			this.Controls.Add(this.panelBuilder);
			this.Controls.Add(this.panelTextEdit);
			this.Controls.Add(this.toolStrip1);
			this.Name = "QueryEditor";
			this.Size = new System.Drawing.Size(455, 142);
			this.toolStrip1.ResumeLayout(false);
			this.toolStrip1.PerformLayout();
			this.panelTextEdit.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ToolStrip toolStrip1;
		private System.Windows.Forms.ToolStripButton toolStripButtonRun;
		private System.Windows.Forms.Panel panelBuilder;
		private System.Windows.Forms.Panel panelTextEdit;
		private System.Windows.Forms.RichTextBox queryTextBox;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripButton toolStripButtonToggleMode;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
		private System.Windows.Forms.ToolStripButton toolStripButtonOpenQuery;
		private System.Windows.Forms.ToolStripButton toolStripButtonSaveQuery;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
		private System.Windows.Forms.ToolStripComboBox comboProjectNames;
		private System.Windows.Forms.LinkLabel resultLabel;
	}
}
