namespace DoW_DE_Mod_Manager
{
    partial class StartExpansionForm
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
            this.startOriginalButton = new System.Windows.Forms.Button();
            this.startWAButton = new System.Windows.Forms.Button();
            this.startDCButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // startOriginalButton
            // 
            this.startOriginalButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.startOriginalButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startOriginalButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.startOriginalButton.Location = new System.Drawing.Point(35, 12);
            this.startOriginalButton.Name = "startOriginalButton";
            this.startOriginalButton.Size = new System.Drawing.Size(129, 29);
            this.startOriginalButton.TabIndex = 0;
            this.startOriginalButton.Text = "Start Original";
            this.startOriginalButton.UseVisualStyleBackColor = false;
            this.startOriginalButton.Click += new System.EventHandler(this.StartOriginalButton_Click);
            // 
            // startWAButton
            // 
            this.startWAButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.startWAButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startWAButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.startWAButton.Location = new System.Drawing.Point(35, 56);
            this.startWAButton.Name = "startWAButton";
            this.startWAButton.Size = new System.Drawing.Size(129, 29);
            this.startWAButton.TabIndex = 1;
            this.startWAButton.Text = "Start Winter Assault";
            this.startWAButton.UseVisualStyleBackColor = false;
            this.startWAButton.Click += new System.EventHandler(this.StartWAButton_Click);
            // 
            // startDCButton
            // 
            this.startDCButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.startDCButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startDCButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.startDCButton.Location = new System.Drawing.Point(35, 101);
            this.startDCButton.Name = "startDCButton";
            this.startDCButton.Size = new System.Drawing.Size(129, 29);
            this.startDCButton.TabIndex = 2;
            this.startDCButton.Text = "Start Dark Crusade";
            this.startDCButton.UseVisualStyleBackColor = false;
            this.startDCButton.Click += new System.EventHandler(this.StartDCButton_Click);
            // 
            // StartExpansionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.ClientSize = new System.Drawing.Size(200, 149);
            this.Controls.Add(this.startDCButton);
            this.Controls.Add(this.startWAButton);
            this.Controls.Add(this.startOriginalButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StartExpansionForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Start Expansion";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.StartExpansionForm_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button startOriginalButton;
        private System.Windows.Forms.Button startWAButton;
        private System.Windows.Forms.Button startDCButton;
    }
}