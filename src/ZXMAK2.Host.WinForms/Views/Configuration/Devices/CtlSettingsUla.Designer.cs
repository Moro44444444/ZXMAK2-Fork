namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
	partial class CtlSettingsUla
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
            this.groupBox = new System.Windows.Forms.GroupBox();
            this.groupBoxAudio = new System.Windows.Forms.GroupBox();
            this.lblMasterVolume = new System.Windows.Forms.Label();
            this.lblMasterPercent = new System.Windows.Forms.Label();
            this.trkMasterVolume = new System.Windows.Forms.TrackBar();
            this.lblType = new System.Windows.Forms.Label();
            this.cbxType = new System.Windows.Forms.ComboBox();
            this.groupBox.SuspendLayout();
            this.groupBoxAudio.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trkMasterVolume)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox
            // 
            this.groupBox.Controls.Add(this.lblType);
            this.groupBox.Controls.Add(this.cbxType);
            this.groupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox.Location = new System.Drawing.Point(0, 0);
            this.groupBox.Name = "groupBox";
            this.groupBox.Size = new System.Drawing.Size(300, 90);
            this.groupBox.TabIndex = 0;
            this.groupBox.TabStop = false;
            this.groupBox.Text = "ULA Settings:";
            //
            // groupBoxAudio
            //
            this.groupBoxAudio.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxAudio.Controls.Add(this.lblMasterVolume);
            this.groupBoxAudio.Controls.Add(this.lblMasterPercent);
            this.groupBoxAudio.Controls.Add(this.trkMasterVolume);
            this.groupBoxAudio.Location = new System.Drawing.Point(0, 228);
            this.groupBoxAudio.Name = "groupBoxAudio";
            this.groupBoxAudio.Size = new System.Drawing.Size(300, 104);
            this.groupBoxAudio.TabIndex = 2;
            this.groupBoxAudio.TabStop = false;
            this.groupBoxAudio.Text = "Audio Output:";
            //
            // lblMasterVolume
            //
            this.lblMasterVolume.AutoSize = true;
            this.lblMasterVolume.Location = new System.Drawing.Point(8, 21);
            this.lblMasterVolume.Name = "lblMasterVolume";
            this.lblMasterVolume.Text = "Master volume:";
            //
            // lblMasterPercent
            //
            this.lblMasterPercent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMasterPercent.Location = new System.Drawing.Point(226, 22);
            this.lblMasterPercent.Name = "lblMasterPercent";
            this.lblMasterPercent.Size = new System.Drawing.Size(48, 16);
            this.lblMasterPercent.Text = "100%";
            this.lblMasterPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // trkMasterVolume
            //
            this.trkMasterVolume.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trkMasterVolume.LargeChange = 10;
            this.trkMasterVolume.Location = new System.Drawing.Point(7, 39);
            this.trkMasterVolume.Maximum = 200;
            this.trkMasterVolume.Name = "trkMasterVolume";
            this.trkMasterVolume.Size = new System.Drawing.Size(269, 45);
            this.trkMasterVolume.TabIndex = 0;
            this.trkMasterVolume.TickFrequency = 25;
            this.trkMasterVolume.Value = 100;
            this.trkMasterVolume.ValueChanged += new System.EventHandler(this.trkMasterVolume_ValueChanged);
            // 
            // lblType
            // 
            this.lblType.AutoSize = true;
            this.lblType.Location = new System.Drawing.Point(6, 25);
            this.lblType.Name = "lblType";
            this.lblType.Size = new System.Drawing.Size(34, 13);
            this.lblType.TabIndex = 1;
            this.lblType.Text = "Type:";
            // 
            // cbxType
            // 
            this.cbxType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxType.FormattingEnabled = true;
            this.cbxType.Location = new System.Drawing.Point(6, 41);
            this.cbxType.Name = "cbxType";
            this.cbxType.Size = new System.Drawing.Size(177, 21);
            this.cbxType.TabIndex = 0;
            // 
            // ControlSettingsUla
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxAudio);
            this.Controls.Add(this.groupBox);
            this.Name = "ControlSettingsUla";
            this.Size = new System.Drawing.Size(300, 332);
            this.groupBox.ResumeLayout(false);
            this.groupBox.PerformLayout();
            this.groupBoxAudio.ResumeLayout(false);
            this.groupBoxAudio.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trkMasterVolume)).EndInit();
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox;
		private System.Windows.Forms.GroupBox groupBoxAudio;
		private System.Windows.Forms.Label lblMasterVolume;
		private System.Windows.Forms.Label lblMasterPercent;
		private System.Windows.Forms.TrackBar trkMasterVolume;
		private System.Windows.Forms.Label lblType;
		private System.Windows.Forms.ComboBox cbxType;
	}
}
