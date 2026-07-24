namespace FG.CheckoutAndBuild2.Controls.Forms
{
    partial class CopySettingsControl
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
            this.buttonCopy = new System.Windows.Forms.Button();
            this.labelSrcProfile = new System.Windows.Forms.Label();
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.comboBoxSourceWorkspace = new System.Windows.Forms.ComboBox();
            this.labelSrcWorkspace = new System.Windows.Forms.Label();
            this.comboBoxSourceProfile = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.comboBoxTargetWorkspace = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxTargetProfile = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.labelAffected = new System.Windows.Forms.Label();
            this.labelResult = new System.Windows.Forms.Label();
            this.groupBoxExport = new System.Windows.Forms.GroupBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.groupBoxCopy = new System.Windows.Forms.GroupBox();
            this.Reset = new System.Windows.Forms.GroupBox();
            this.buttonReset = new System.Windows.Forms.Button();
            this.groupBoxSource.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBoxExport.SuspendLayout();
            this.groupBoxCopy.SuspendLayout();
            this.Reset.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonCopy
            // 
            this.buttonCopy.Location = new System.Drawing.Point(185, 175);
            this.buttonCopy.Name = "buttonCopy";
            this.buttonCopy.Size = new System.Drawing.Size(138, 23);
            this.buttonCopy.TabIndex = 0;
            this.buttonCopy.Text = "Copy Settings";
            this.buttonCopy.UseVisualStyleBackColor = true;
            this.buttonCopy.Click += new System.EventHandler(this.button1_Click);
            // 
            // labelSrcProfile
            // 
            this.labelSrcProfile.AutoSize = true;
            this.labelSrcProfile.Location = new System.Drawing.Point(41, 27);
            this.labelSrcProfile.Name = "labelSrcProfile";
            this.labelSrcProfile.Size = new System.Drawing.Size(39, 13);
            this.labelSrcProfile.TabIndex = 3;
            this.labelSrcProfile.Text = "Profile:";
            // 
            // groupBoxSource
            // 
            this.groupBoxSource.Controls.Add(this.comboBoxSourceWorkspace);
            this.groupBoxSource.Controls.Add(this.labelSrcWorkspace);
            this.groupBoxSource.Controls.Add(this.comboBoxSourceProfile);
            this.groupBoxSource.Controls.Add(this.labelSrcProfile);
            this.groupBoxSource.Location = new System.Drawing.Point(3, 52);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Size = new System.Drawing.Size(246, 106);
            this.groupBoxSource.TabIndex = 1;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "Source";
            // 
            // comboBoxSourceWorkspace
            // 
            this.comboBoxSourceWorkspace.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxSourceWorkspace.DisplayMember = "Name";
            this.comboBoxSourceWorkspace.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSourceWorkspace.FormattingEnabled = true;
            this.comboBoxSourceWorkspace.Location = new System.Drawing.Point(86, 51);
            this.comboBoxSourceWorkspace.Name = "comboBoxSourceWorkspace";
            this.comboBoxSourceWorkspace.Size = new System.Drawing.Size(143, 21);
            this.comboBoxSourceWorkspace.TabIndex = 6;
            this.comboBoxSourceWorkspace.SelectedValueChanged += new System.EventHandler(this.Combobox_SelectedValueChanged);
            // 
            // labelSrcWorkspace
            // 
            this.labelSrcWorkspace.AutoSize = true;
            this.labelSrcWorkspace.Location = new System.Drawing.Point(15, 54);
            this.labelSrcWorkspace.Name = "labelSrcWorkspace";
            this.labelSrcWorkspace.Size = new System.Drawing.Size(65, 13);
            this.labelSrcWorkspace.TabIndex = 5;
            this.labelSrcWorkspace.Text = "Workspace:";
            // 
            // comboBoxSourceProfile
            // 
            this.comboBoxSourceProfile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxSourceProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSourceProfile.FormattingEnabled = true;
            this.comboBoxSourceProfile.Location = new System.Drawing.Point(86, 24);
            this.comboBoxSourceProfile.Name = "comboBoxSourceProfile";
            this.comboBoxSourceProfile.Size = new System.Drawing.Size(143, 21);
            this.comboBoxSourceProfile.TabIndex = 4;
            this.comboBoxSourceProfile.SelectedValueChanged += new System.EventHandler(this.Combobox_SelectedValueChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.comboBoxTargetWorkspace);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.comboBoxTargetProfile);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(263, 52);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(317, 106);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Target";
            // 
            // comboBoxTargetWorkspace
            // 
            this.comboBoxTargetWorkspace.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxTargetWorkspace.DisplayMember = "Name";
            this.comboBoxTargetWorkspace.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTargetWorkspace.FormattingEnabled = true;
            this.comboBoxTargetWorkspace.Location = new System.Drawing.Point(86, 51);
            this.comboBoxTargetWorkspace.Name = "comboBoxTargetWorkspace";
            this.comboBoxTargetWorkspace.Size = new System.Drawing.Size(214, 21);
            this.comboBoxTargetWorkspace.TabIndex = 6;
            this.comboBoxTargetWorkspace.SelectedValueChanged += new System.EventHandler(this.Combobox_SelectedValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 54);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Workspace:";
            // 
            // comboBoxTargetProfile
            // 
            this.comboBoxTargetProfile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxTargetProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTargetProfile.FormattingEnabled = true;
            this.comboBoxTargetProfile.Location = new System.Drawing.Point(86, 24);
            this.comboBoxTargetProfile.Name = "comboBoxTargetProfile";
            this.comboBoxTargetProfile.Size = new System.Drawing.Size(214, 21);
            this.comboBoxTargetProfile.TabIndex = 4;
            this.comboBoxTargetProfile.SelectedValueChanged += new System.EventHandler(this.Combobox_SelectedValueChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(41, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(39, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Profile:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(49, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(409, 16);
            this.label3.TabIndex = 8;
            this.label3.Text = "Here you can copy your Settings between Profiles and Workspaces";
            // 
            // labelAffected
            // 
            this.labelAffected.AutoSize = true;
            this.labelAffected.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelAffected.ForeColor = System.Drawing.Color.Maroon;
            this.labelAffected.Location = new System.Drawing.Point(182, 175);
            this.labelAffected.Name = "labelAffected";
            this.labelAffected.Size = new System.Drawing.Size(0, 15);
            this.labelAffected.TabIndex = 9;
            // 
            // labelResult
            // 
            this.labelResult.AutoSize = true;
            this.labelResult.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelResult.Location = new System.Drawing.Point(158, 210);
            this.labelResult.Name = "labelResult";
            this.labelResult.Size = new System.Drawing.Size(185, 13);
            this.labelResult.TabIndex = 10;
            this.labelResult.Text = "Your Settings has been copied!";
            this.labelResult.Visible = false;
            // 
            // groupBoxExport
            // 
            this.groupBoxExport.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxExport.Controls.Add(this.Reset);
            this.groupBoxExport.Controls.Add(this.button2);
            this.groupBoxExport.Controls.Add(this.button1);
            this.groupBoxExport.Location = new System.Drawing.Point(0, 281);
            this.groupBoxExport.Name = "groupBoxExport";
            this.groupBoxExport.Size = new System.Drawing.Size(596, 115);
            this.groupBoxExport.TabIndex = 11;
            this.groupBoxExport.TabStop = false;
            this.groupBoxExport.Text = "Import / Export";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(126, 35);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(87, 28);
            this.button2.TabIndex = 1;
            this.button2.Text = "Import...";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(21, 35);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(87, 28);
            this.button1.TabIndex = 0;
            this.button1.Text = "Export...";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // groupBoxCopy
            // 
            this.groupBoxCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxCopy.Controls.Add(this.label3);
            this.groupBoxCopy.Controls.Add(this.buttonCopy);
            this.groupBoxCopy.Controls.Add(this.labelResult);
            this.groupBoxCopy.Controls.Add(this.labelAffected);
            this.groupBoxCopy.Controls.Add(this.groupBoxSource);
            this.groupBoxCopy.Controls.Add(this.groupBox1);
            this.groupBoxCopy.Location = new System.Drawing.Point(0, 0);
            this.groupBoxCopy.Name = "groupBoxCopy";
            this.groupBoxCopy.Size = new System.Drawing.Size(596, 264);
            this.groupBoxCopy.TabIndex = 12;
            this.groupBoxCopy.TabStop = false;
            this.groupBoxCopy.Text = "Copy Settings";
            // 
            // Reset
            // 
            this.Reset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Reset.Controls.Add(this.buttonReset);
            this.Reset.Location = new System.Drawing.Point(272, 0);
            this.Reset.Name = "Reset";
            this.Reset.Size = new System.Drawing.Size(324, 114);
            this.Reset.TabIndex = 11;
            this.Reset.TabStop = false;
            this.Reset.Text = "Reset";
            // 
            // buttonReset
            // 
            this.buttonReset.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonReset.Location = new System.Drawing.Point(18, 35);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(290, 31);
            this.buttonReset.TabIndex = 0;
            this.buttonReset.Text = "Reset All Settings...";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // CopySettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxCopy);
            this.Controls.Add(this.groupBoxExport);
            this.Name = "CopySettingsControl";
            this.Size = new System.Drawing.Size(596, 396);
            this.groupBoxSource.ResumeLayout(false);
            this.groupBoxSource.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBoxExport.ResumeLayout(false);
            this.groupBoxCopy.ResumeLayout(false);
            this.groupBoxCopy.PerformLayout();
            this.Reset.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonCopy;
        private System.Windows.Forms.Label labelSrcProfile;
        private System.Windows.Forms.GroupBox groupBoxSource;
        private System.Windows.Forms.ComboBox comboBoxSourceWorkspace;
        private System.Windows.Forms.Label labelSrcWorkspace;
        private System.Windows.Forms.ComboBox comboBoxSourceProfile;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox comboBoxTargetWorkspace;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxTargetProfile;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label labelAffected;
        private System.Windows.Forms.Label labelResult;
        private System.Windows.Forms.GroupBox groupBoxExport;
        private System.Windows.Forms.Button button1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.GroupBox groupBoxCopy;
        private System.Windows.Forms.GroupBox Reset;
        private System.Windows.Forms.Button buttonReset;
    }
}
