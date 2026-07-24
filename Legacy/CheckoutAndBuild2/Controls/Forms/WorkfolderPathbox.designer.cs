using System;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	partial class WorkfolderPathbox
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
			this.buttonBrowse = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// textBoxValue
			// 
			// 
			// buttonBrowse
			// 
			this.buttonBrowse.Dock = System.Windows.Forms.DockStyle.Right;
			this.buttonBrowse.Location = new System.Drawing.Point(246, 0);
			this.buttonBrowse.Name = "buttonBrowse";
			this.buttonBrowse.Size = new System.Drawing.Size(22, 20);
			this.buttonBrowse.TabIndex = 1;
			this.buttonBrowse.Text = "...";
			this.buttonBrowse.UseVisualStyleBackColor = true;
			// 
			// WorkfolderPathbox
			// 

			this.Controls.Add(this.buttonBrowse);
			this.Name = "WorkfolderPathbox";
			this.Size = new System.Drawing.Size(268, 20);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button buttonBrowse;
	}
}
