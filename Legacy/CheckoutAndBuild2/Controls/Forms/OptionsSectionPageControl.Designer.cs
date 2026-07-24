namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class OptionsSectionPageControl
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
            this.sectionsGroup = new System.Windows.Forms.GroupBox();
            this.listBoxSection = new System.Windows.Forms.ListBox();
            this.groupBoxVisibility = new System.Windows.Forms.GroupBox();
            this.listViewIncludedPages = new System.Windows.Forms.ListView();
            this.checkBoxManageAllSection = new System.Windows.Forms.CheckBox();
            this.panelLoading = new System.Windows.Forms.Panel();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBoxSectionManagementEnabled = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panelNotification = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.sectionsGroup.SuspendLayout();
            this.groupBoxVisibility.SuspendLayout();
            this.panelLoading.SuspendLayout();
            this.panelNotification.SuspendLayout();
            this.SuspendLayout();
            // 
            // sectionsGroup
            // 
            this.sectionsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.sectionsGroup.Controls.Add(this.listBoxSection);
            this.sectionsGroup.Location = new System.Drawing.Point(0, 71);
            this.sectionsGroup.Name = "sectionsGroup";
            this.sectionsGroup.Size = new System.Drawing.Size(197, 394);
            this.sectionsGroup.TabIndex = 0;
            this.sectionsGroup.TabStop = false;
            this.sectionsGroup.Text = "Sections";
            // 
            // listBoxSection
            // 
            this.listBoxSection.DisplayMember = "Name";
            this.listBoxSection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxSection.FormattingEnabled = true;
            this.listBoxSection.Location = new System.Drawing.Point(3, 16);
            this.listBoxSection.Name = "listBoxSection";
            this.listBoxSection.Size = new System.Drawing.Size(191, 375);
            this.listBoxSection.TabIndex = 0;
            this.listBoxSection.SelectedIndexChanged += new System.EventHandler(this.listBoxSection_SelectedIndexChanged);
            // 
            // groupBoxVisibility
            // 
            this.groupBoxVisibility.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxVisibility.Controls.Add(this.listViewIncludedPages);
            this.groupBoxVisibility.Location = new System.Drawing.Point(201, 71);
            this.groupBoxVisibility.Name = "groupBoxVisibility";
            this.groupBoxVisibility.Size = new System.Drawing.Size(261, 394);
            this.groupBoxVisibility.TabIndex = 2;
            this.groupBoxVisibility.TabStop = false;
            this.groupBoxVisibility.Text = "Visible in Pages";
            // 
            // listViewIncludedPages
            // 
            this.listViewIncludedPages.CheckBoxes = true;
            this.listViewIncludedPages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewIncludedPages.Location = new System.Drawing.Point(3, 16);
            this.listViewIncludedPages.Name = "listViewIncludedPages";
            this.listViewIncludedPages.Size = new System.Drawing.Size(255, 375);
            this.listViewIncludedPages.TabIndex = 0;
            this.listViewIncludedPages.UseCompatibleStateImageBehavior = false;
            this.listViewIncludedPages.View = System.Windows.Forms.View.List;
            // 
            // checkBoxManageAllSection
            // 
            this.checkBoxManageAllSection.AutoSize = true;
            this.checkBoxManageAllSection.Location = new System.Drawing.Point(1, 48);
            this.checkBoxManageAllSection.Name = "checkBoxManageAllSection";
            this.checkBoxManageAllSection.Size = new System.Drawing.Size(462, 17);
            this.checkBoxManageAllSection.TabIndex = 0;
            this.checkBoxManageAllSection.Text = "Manage all VisualStudio TeamExplorer Sections (otherwise CheckoutAndBuild Section" +
    "s only)";
            this.checkBoxManageAllSection.UseVisualStyleBackColor = true;
            this.checkBoxManageAllSection.CheckedChanged += new System.EventHandler(this.checkBoxManageAllSection_CheckedChanged);
            // 
            // panelLoading
            // 
            this.panelLoading.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelLoading.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panelLoading.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelLoading.Controls.Add(this.progressBar);
            this.panelLoading.Controls.Add(this.label1);
            this.panelLoading.Location = new System.Drawing.Point(98, 171);
            this.panelLoading.Name = "panelLoading";
            this.panelLoading.Size = new System.Drawing.Size(274, 72);
            this.panelLoading.TabIndex = 1;
            this.panelLoading.Visible = false;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(17, 31);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(243, 23);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Please Wait...";
            // 
            // checkBoxSectionManagementEnabled
            // 
            this.checkBoxSectionManagementEnabled.AutoSize = true;
            this.checkBoxSectionManagementEnabled.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBoxSectionManagementEnabled.Location = new System.Drawing.Point(1, 13);
            this.checkBoxSectionManagementEnabled.Name = "checkBoxSectionManagementEnabled";
            this.checkBoxSectionManagementEnabled.Size = new System.Drawing.Size(425, 17);
            this.checkBoxSectionManagementEnabled.TabIndex = 4;
            this.checkBoxSectionManagementEnabled.Text = "Allow Section Management (can make problems with other Extensions)";
            this.checkBoxSectionManagementEnabled.UseVisualStyleBackColor = true;
            this.checkBoxSectionManagementEnabled.CheckedChanged += new System.EventHandler(this.checkBoxSectionManagementEnabled_CheckedChanged);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.DarkGray;
            this.panel1.Location = new System.Drawing.Point(0, 36);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(460, 1);
            this.panel1.TabIndex = 5;
            // 
            // panelNotification
            // 
            this.panelNotification.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelNotification.BackColor = System.Drawing.SystemColors.Info;
            this.panelNotification.Controls.Add(this.label2);
            this.panelNotification.Location = new System.Drawing.Point(0, 414);
            this.panelNotification.Name = "panelNotification";
            this.panelNotification.Size = new System.Drawing.Size(463, 47);
            this.panelNotification.TabIndex = 1;
            this.panelNotification.Visible = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.SystemColors.HotTrack;
            this.label2.Location = new System.Drawing.Point(3, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(402, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "You must restart Visual Studio in order for the changes to take effect.";
            // 
            // OptionsSectionPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelNotification);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.checkBoxSectionManagementEnabled);
            this.Controls.Add(this.checkBoxManageAllSection);
            this.Controls.Add(this.panelLoading);
            this.Controls.Add(this.sectionsGroup);
            this.Controls.Add(this.groupBoxVisibility);
            this.Name = "OptionsSectionPageControl";
            this.Size = new System.Drawing.Size(466, 465);
            this.sectionsGroup.ResumeLayout(false);
            this.groupBoxVisibility.ResumeLayout(false);
            this.panelLoading.ResumeLayout(false);
            this.panelLoading.PerformLayout();
            this.panelNotification.ResumeLayout(false);
            this.panelNotification.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.GroupBox sectionsGroup;
		private System.Windows.Forms.ListBox listBoxSection;
		private System.Windows.Forms.GroupBox groupBoxVisibility;
		private System.Windows.Forms.ListView listViewIncludedPages;
		private System.Windows.Forms.CheckBox checkBoxManageAllSection;
		private System.Windows.Forms.Panel panelLoading;
		private System.Windows.Forms.ProgressBar progressBar;
		private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxSectionManagementEnabled;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panelNotification;
        private System.Windows.Forms.Label label2;
    }
}
