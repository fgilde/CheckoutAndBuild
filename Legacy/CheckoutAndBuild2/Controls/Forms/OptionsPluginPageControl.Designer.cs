namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class OptionsPluginPageControl
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.buttonRemove = new System.Windows.Forms.Button();
			this.buttonAdd = new System.Windows.Forms.Button();
			this.listBoxPlugins = new System.Windows.Forms.ListBox();
			this.panelTop = new System.Windows.Forms.Panel();
			this.linkLabel = new System.Windows.Forms.LinkLabel();
			this.label1 = new System.Windows.Forms.Label();
			this.linkLabelNotification = new System.Windows.Forms.LinkLabel();
			this.sectionsGroup.SuspendLayout();
			this.panel1.SuspendLayout();
			this.panelTop.SuspendLayout();
			this.SuspendLayout();
			// 
			// sectionsGroup
			// 
			this.sectionsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.sectionsGroup.Controls.Add(this.panel1);
			this.sectionsGroup.Controls.Add(this.listBoxPlugins);
			this.sectionsGroup.Location = new System.Drawing.Point(0, 65);
			this.sectionsGroup.Name = "sectionsGroup";
			this.sectionsGroup.Size = new System.Drawing.Size(463, 400);
			this.sectionsGroup.TabIndex = 0;
			this.sectionsGroup.TabStop = false;
			this.sectionsGroup.Text = "Plugins";
			// 
			// panel1
			// 
			this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.panel1.Controls.Add(this.linkLabelNotification);
			this.panel1.Controls.Add(this.buttonRemove);
			this.panel1.Controls.Add(this.buttonAdd);
			this.panel1.Location = new System.Drawing.Point(3, 352);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(457, 42);
			this.panel1.TabIndex = 1;
			// 
			// buttonRemove
			// 
			this.buttonRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonRemove.Enabled = false;
			this.buttonRemove.Location = new System.Drawing.Point(378, 7);
			this.buttonRemove.Name = "buttonRemove";
			this.buttonRemove.Size = new System.Drawing.Size(75, 25);
			this.buttonRemove.TabIndex = 1;
			this.buttonRemove.Text = "Remove";
			this.buttonRemove.UseVisualStyleBackColor = true;
			this.buttonRemove.Visible = false;
			this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
			// 
			// buttonAdd
			// 
			this.buttonAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonAdd.Location = new System.Drawing.Point(378, 7);
			this.buttonAdd.Name = "buttonAdd";
			this.buttonAdd.Size = new System.Drawing.Size(75, 25);
			this.buttonAdd.TabIndex = 0;
			this.buttonAdd.Text = "Add Plugin";
			this.buttonAdd.UseVisualStyleBackColor = true;
			this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
			// 
			// listBoxPlugins
			// 
			this.listBoxPlugins.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxPlugins.DisplayMember = "Name";
			this.listBoxPlugins.FormattingEnabled = true;
			this.listBoxPlugins.Location = new System.Drawing.Point(3, 16);
			this.listBoxPlugins.Name = "listBoxPlugins";
			this.listBoxPlugins.Size = new System.Drawing.Size(457, 329);
			this.listBoxPlugins.TabIndex = 0;
			this.listBoxPlugins.SelectedIndexChanged += new System.EventHandler(this.listBoxPlugins_SelectedIndexChanged);
			// 
			// panelTop
			// 
			this.panelTop.Controls.Add(this.linkLabel);
			this.panelTop.Controls.Add(this.label1);
			this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
			this.panelTop.Location = new System.Drawing.Point(0, 0);
			this.panelTop.Name = "panelTop";
			this.panelTop.Size = new System.Drawing.Size(466, 59);
			this.panelTop.TabIndex = 3;
			// 
			// linkLabel
			// 
			this.linkLabel.AutoEllipsis = true;
			this.linkLabel.AutoSize = true;
			this.linkLabel.Location = new System.Drawing.Point(11, 24);
			this.linkLabel.Name = "linkLabel";
			this.linkLabel.Size = new System.Drawing.Size(55, 13);
			this.linkLabel.TabIndex = 2;
			this.linkLabel.TabStop = true;
			this.linkLabel.Text = "linkLabel1";
			this.linkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(10, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(84, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Plugin Directory:";
			// 
			// linkLabelNotification
			// 
			this.linkLabelNotification.AutoSize = true;
			this.linkLabelNotification.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.linkLabelNotification.Location = new System.Drawing.Point(8, 13);
			this.linkLabelNotification.Name = "linkLabelNotification";
			this.linkLabelNotification.Size = new System.Drawing.Size(212, 13);
			this.linkLabelNotification.TabIndex = 3;
			this.linkLabelNotification.TabStop = true;
			this.linkLabelNotification.Text = "! Please Restart your Visual Studio !";
			this.linkLabelNotification.Visible = false;
			this.linkLabelNotification.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelNotification_LinkClicked);
			// 
			// OptionsPluginPageControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.panelTop);
			this.Controls.Add(this.sectionsGroup);
			this.Name = "OptionsPluginPageControl";
			this.Size = new System.Drawing.Size(466, 465);
			this.sectionsGroup.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panelTop.ResumeLayout(false);
			this.panelTop.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox sectionsGroup;
		private System.Windows.Forms.ListBox listBoxPlugins;
		private System.Windows.Forms.Panel panelTop;
		private System.Windows.Forms.LinkLabel linkLabel;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Button buttonRemove;
		private System.Windows.Forms.Button buttonAdd;
		private System.Windows.Forms.LinkLabel linkLabelNotification;


	}
}
