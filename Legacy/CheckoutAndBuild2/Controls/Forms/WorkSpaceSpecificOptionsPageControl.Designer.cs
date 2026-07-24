namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class WorkSpaceSpecificOptionsPageControl
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
            this.panelMain = new System.Windows.Forms.Panel();
            this.elementHost = new FG.CheckoutAndBuild2.Controls.Forms.AutoEnabledElementHost();
            this.panelBottomMenuBar = new System.Windows.Forms.Panel();
            this.comboBoxWorkspaceSelect = new System.Windows.Forms.ComboBox();
            this.panelMain.SuspendLayout();
            this.panelBottomMenuBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.panelBottomMenuBar);
            this.panelMain.Controls.Add(this.elementHost);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(466, 465);
            this.panelMain.TabIndex = 2;
            // 
            // elementHost
            // 
            this.elementHost.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.elementHost.Location = new System.Drawing.Point(3, 35);
            this.elementHost.Name = "elementHost";
            this.elementHost.Size = new System.Drawing.Size(452, 427);
            this.elementHost.TabIndex = 2;
            this.elementHost.Text = "elementHost1";
            this.elementHost.Child = null;
            // 
            // panelBottomMenuBar
            // 
            this.panelBottomMenuBar.Controls.Add(this.comboBoxWorkspaceSelect);
            this.panelBottomMenuBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelBottomMenuBar.Location = new System.Drawing.Point(0, 0);
            this.panelBottomMenuBar.Name = "panelBottomMenuBar";
            this.panelBottomMenuBar.Size = new System.Drawing.Size(466, 29);
            this.panelBottomMenuBar.TabIndex = 1;
            // 
            // comboBoxWorkspaceSelect
            // 
            this.comboBoxWorkspaceSelect.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxWorkspaceSelect.DisplayMember = "Name";
            this.comboBoxWorkspaceSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxWorkspaceSelect.FormattingEnabled = true;
            this.comboBoxWorkspaceSelect.Location = new System.Drawing.Point(3, 4);
            this.comboBoxWorkspaceSelect.Name = "comboBoxWorkspaceSelect";
            this.comboBoxWorkspaceSelect.Size = new System.Drawing.Size(452, 21);
            this.comboBoxWorkspaceSelect.TabIndex = 0;
            this.comboBoxWorkspaceSelect.SelectedIndexChanged += new System.EventHandler(this.comboBoxWorkspaceSelect_SelectedIndexChanged);
            // 
            // WorkSpaceSpecificOptionsPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelMain);
            this.Name = "WorkSpaceSpecificOptionsPageControl";
            this.Size = new System.Drawing.Size(466, 465);
            this.panelMain.ResumeLayout(false);
            this.panelBottomMenuBar.ResumeLayout(false);
            this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Panel panelMain;
        private AutoEnabledElementHost elementHost;
        private System.Windows.Forms.Panel panelBottomMenuBar;
        private System.Windows.Forms.ComboBox comboBoxWorkspaceSelect;
    }
}
