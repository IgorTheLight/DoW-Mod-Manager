﻿namespace DoW_DE_Nod_Manager
{
    partial class SystemPerformanceManagerForm
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
            this.minimumTimerResolutionLabel = new System.Windows.Forms.Label();
            this.setTimerResolutionButton = new System.Windows.Forms.Button();
            this.defaultTimerResolutionButton = new System.Windows.Forms.Button();
            this.currentTimerResolutionLabel = new System.Windows.Forms.Label();
            this.currentTimerResolutionTextBox = new System.Windows.Forms.TextBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.compatibilityTabPage = new System.Windows.Forms.TabPage();
            this.openDoWPropertiesButton = new System.Windows.Forms.Button();
            this.fullscreenOptimizationsCheckBox = new System.Windows.Forms.CheckBox();
            this.fullscreenOptimizationsLabel = new System.Windows.Forms.Label();
            this.setPropertiesButton = new System.Windows.Forms.Button();
            this.HDPIiScalingCheckBox = new System.Windows.Forms.CheckBox();
            this.HDPIScalingLabel = new System.Windows.Forms.Label();
            this.runAsAdministratorCheckBox = new System.Windows.Forms.CheckBox();
            this.runAsAdministratorLabel = new System.Windows.Forms.Label();
            this.comatibilityModeCheckBox = new System.Windows.Forms.CheckBox();
            this.compatibilityModeLabel = new System.Windows.Forms.Label();
            this.powerSettingsTabPage = new System.Windows.Forms.TabPage();
            this.powerOptionsButton = new System.Windows.Forms.Button();
            this.unlockUltimatePerformanceButton = new System.Windows.Forms.Button();
            this.setPowerPlanButton = new System.Windows.Forms.Button();
            this.powerPlanComboBox = new System.Windows.Forms.ComboBox();
            this.powerPlanLabel = new System.Windows.Forms.Label();
            this.timerResolutionTabPage = new System.Windows.Forms.TabPage();
            this.minimumTimerResolutionTextBox = new System.Windows.Forms.TextBox();
            this.maximumTimerResolutionTextBox = new System.Windows.Forms.TextBox();
            this.maximumTimerResolutionLabel = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.compatibilityTabPage.SuspendLayout();
            this.powerSettingsTabPage.SuspendLayout();
            this.timerResolutionTabPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // minimumTimerResolutionLabel
            // 
            this.minimumTimerResolutionLabel.AutoSize = true;
            this.minimumTimerResolutionLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.minimumTimerResolutionLabel.Location = new System.Drawing.Point(15, 14);
            this.minimumTimerResolutionLabel.Name = "minimumTimerResolutionLabel";
            this.minimumTimerResolutionLabel.Size = new System.Drawing.Size(107, 13);
            this.minimumTimerResolutionLabel.TabIndex = 0;
            this.minimumTimerResolutionLabel.Text = "Minimum Resolution: ";
            // 
            // setTimerResolutionButton
            // 
            this.setTimerResolutionButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.setTimerResolutionButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.setTimerResolutionButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.setTimerResolutionButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.setTimerResolutionButton.Location = new System.Drawing.Point(22, 200);
            this.setTimerResolutionButton.Name = "setTimerResolutionButton";
            this.setTimerResolutionButton.Size = new System.Drawing.Size(120, 38);
            this.setTimerResolutionButton.TabIndex = 2;
            this.setTimerResolutionButton.Text = "Lower Timer Resolution";
            this.setTimerResolutionButton.UseVisualStyleBackColor = false;
            this.setTimerResolutionButton.Click += new System.EventHandler(this.SetTimerResolutionButton_Click);
            // 
            // defaultTimerResolutionButton
            // 
            this.defaultTimerResolutionButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultTimerResolutionButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.defaultTimerResolutionButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.defaultTimerResolutionButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.defaultTimerResolutionButton.Location = new System.Drawing.Point(182, 200);
            this.defaultTimerResolutionButton.Name = "defaultTimerResolutionButton";
            this.defaultTimerResolutionButton.Size = new System.Drawing.Size(120, 38);
            this.defaultTimerResolutionButton.TabIndex = 3;
            this.defaultTimerResolutionButton.Text = "Default Timer Resolution";
            this.defaultTimerResolutionButton.UseVisualStyleBackColor = false;
            this.defaultTimerResolutionButton.Click += new System.EventHandler(this.DefaultTimerResolutionButton_Click);
            // 
            // currentTimerResolutionLabel
            // 
            this.currentTimerResolutionLabel.AutoSize = true;
            this.currentTimerResolutionLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.currentTimerResolutionLabel.Location = new System.Drawing.Point(16, 83);
            this.currentTimerResolutionLabel.Name = "currentTimerResolutionLabel";
            this.currentTimerResolutionLabel.Size = new System.Drawing.Size(126, 13);
            this.currentTimerResolutionLabel.TabIndex = 4;
            this.currentTimerResolutionLabel.Text = "Current Timer Resolution:";
            // 
            // currentTimerResolutionTextBox
            // 
            this.currentTimerResolutionTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.currentTimerResolutionTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.currentTimerResolutionTextBox.Location = new System.Drawing.Point(147, 80);
            this.currentTimerResolutionTextBox.Name = "currentTimerResolutionTextBox";
            this.currentTimerResolutionTextBox.ReadOnly = true;
            this.currentTimerResolutionTextBox.Size = new System.Drawing.Size(102, 20);
            this.currentTimerResolutionTextBox.TabIndex = 5;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.compatibilityTabPage);
            this.tabControl1.Controls.Add(this.powerSettingsTabPage);
            this.tabControl1.Controls.Add(this.timerResolutionTabPage);
            this.tabControl1.Location = new System.Drawing.Point(16, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(329, 278);
            this.tabControl1.TabIndex = 6;
            // 
            // compatibilityTabPage
            // 
            this.compatibilityTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.compatibilityTabPage.Controls.Add(this.openDoWPropertiesButton);
            this.compatibilityTabPage.Controls.Add(this.fullscreenOptimizationsCheckBox);
            this.compatibilityTabPage.Controls.Add(this.fullscreenOptimizationsLabel);
            this.compatibilityTabPage.Controls.Add(this.setPropertiesButton);
            this.compatibilityTabPage.Controls.Add(this.HDPIiScalingCheckBox);
            this.compatibilityTabPage.Controls.Add(this.HDPIScalingLabel);
            this.compatibilityTabPage.Controls.Add(this.runAsAdministratorCheckBox);
            this.compatibilityTabPage.Controls.Add(this.runAsAdministratorLabel);
            this.compatibilityTabPage.Controls.Add(this.comatibilityModeCheckBox);
            this.compatibilityTabPage.Controls.Add(this.compatibilityModeLabel);
            this.compatibilityTabPage.Location = new System.Drawing.Point(4, 22);
            this.compatibilityTabPage.Name = "compatibilityTabPage";
            this.compatibilityTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.compatibilityTabPage.Size = new System.Drawing.Size(321, 252);
            this.compatibilityTabPage.TabIndex = 0;
            this.compatibilityTabPage.Text = "Compatibility";
            // 
            // openDoWPropertiesButton
            // 
            this.openDoWPropertiesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.openDoWPropertiesButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.openDoWPropertiesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.openDoWPropertiesButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.openDoWPropertiesButton.Location = new System.Drawing.Point(178, 200);
            this.openDoWPropertiesButton.Name = "openDoWPropertiesButton";
            this.openDoWPropertiesButton.Size = new System.Drawing.Size(120, 38);
            this.openDoWPropertiesButton.TabIndex = 59;
            this.openDoWPropertiesButton.Text = "Open DoW Properties";
            this.openDoWPropertiesButton.UseVisualStyleBackColor = false;
            this.openDoWPropertiesButton.Click += new System.EventHandler(this.OpenDoWPropertiesButton_Click);
            // 
            // fullscreenOptimizationsCheckBox
            // 
            this.fullscreenOptimizationsCheckBox.AutoSize = true;
            this.fullscreenOptimizationsCheckBox.Location = new System.Drawing.Point(229, 124);
            this.fullscreenOptimizationsCheckBox.Name = "fullscreenOptimizationsCheckBox";
            this.fullscreenOptimizationsCheckBox.Size = new System.Drawing.Size(15, 14);
            this.fullscreenOptimizationsCheckBox.TabIndex = 58;
            this.fullscreenOptimizationsCheckBox.UseVisualStyleBackColor = true;
            // 
            // fullscreenOptimizationsLabel
            // 
            this.fullscreenOptimizationsLabel.AutoSize = true;
            this.fullscreenOptimizationsLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.fullscreenOptimizationsLabel.Location = new System.Drawing.Point(16, 124);
            this.fullscreenOptimizationsLabel.Name = "fullscreenOptimizationsLabel";
            this.fullscreenOptimizationsLabel.Size = new System.Drawing.Size(158, 13);
            this.fullscreenOptimizationsLabel.TabIndex = 57;
            this.fullscreenOptimizationsLabel.Text = "Disable fullscreen Optimizations:";
            // 
            // setPropertiesButton
            // 
            this.setPropertiesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.setPropertiesButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.setPropertiesButton.Enabled = false;
            this.setPropertiesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.setPropertiesButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.setPropertiesButton.Location = new System.Drawing.Point(21, 200);
            this.setPropertiesButton.Name = "setPropertiesButton";
            this.setPropertiesButton.Size = new System.Drawing.Size(120, 38);
            this.setPropertiesButton.TabIndex = 56;
            this.setPropertiesButton.Text = "Set properties";
            this.setPropertiesButton.UseVisualStyleBackColor = false;
            this.setPropertiesButton.Click += new System.EventHandler(this.SetPropertiesButton_Click);
            // 
            // HDPIiScalingCheckBox
            // 
            this.HDPIiScalingCheckBox.AutoSize = true;
            this.HDPIiScalingCheckBox.Location = new System.Drawing.Point(229, 49);
            this.HDPIiScalingCheckBox.Name = "HDPIiScalingCheckBox";
            this.HDPIiScalingCheckBox.Size = new System.Drawing.Size(15, 14);
            this.HDPIiScalingCheckBox.TabIndex = 55;
            this.HDPIiScalingCheckBox.UseVisualStyleBackColor = true;
            // 
            // HDPIScalingLabel
            // 
            this.HDPIScalingLabel.AutoSize = true;
            this.HDPIScalingLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.HDPIScalingLabel.Location = new System.Drawing.Point(16, 49);
            this.HDPIScalingLabel.Name = "HDPIScalingLabel";
            this.HDPIScalingLabel.Size = new System.Drawing.Size(179, 13);
            this.HDPIScalingLabel.TabIndex = 54;
            this.HDPIScalingLabel.Text = "Let Application handle HDPI Scaling";
            // 
            // runAsAdministratorCheckBox
            // 
            this.runAsAdministratorCheckBox.AutoSize = true;
            this.runAsAdministratorCheckBox.Location = new System.Drawing.Point(229, 14);
            this.runAsAdministratorCheckBox.Name = "runAsAdministratorCheckBox";
            this.runAsAdministratorCheckBox.Size = new System.Drawing.Size(15, 14);
            this.runAsAdministratorCheckBox.TabIndex = 53;
            this.runAsAdministratorCheckBox.UseVisualStyleBackColor = true;
            // 
            // runAsAdministratorLabel
            // 
            this.runAsAdministratorLabel.AutoSize = true;
            this.runAsAdministratorLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.runAsAdministratorLabel.Location = new System.Drawing.Point(16, 14);
            this.runAsAdministratorLabel.Name = "runAsAdministratorLabel";
            this.runAsAdministratorLabel.Size = new System.Drawing.Size(104, 13);
            this.runAsAdministratorLabel.TabIndex = 52;
            this.runAsAdministratorLabel.Text = "Run as Administrator";
            // 
            // comatibilityModeCheckBox
            // 
            this.comatibilityModeCheckBox.AutoSize = true;
            this.comatibilityModeCheckBox.Location = new System.Drawing.Point(229, 86);
            this.comatibilityModeCheckBox.Name = "comatibilityModeCheckBox";
            this.comatibilityModeCheckBox.Size = new System.Drawing.Size(15, 14);
            this.comatibilityModeCheckBox.TabIndex = 51;
            this.comatibilityModeCheckBox.UseVisualStyleBackColor = true;
            // 
            // compatibilityModeLabel
            // 
            this.compatibilityModeLabel.AutoSize = true;
            this.compatibilityModeLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.compatibilityModeLabel.Location = new System.Drawing.Point(16, 86);
            this.compatibilityModeLabel.Name = "compatibilityModeLabel";
            this.compatibilityModeLabel.Size = new System.Drawing.Size(197, 13);
            this.compatibilityModeLabel.TabIndex = 5;
            this.compatibilityModeLabel.Text = "Compatibility Mode for Windows XP SP2";
            // 
            // powerSettingsTabPage
            // 
            this.powerSettingsTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.powerSettingsTabPage.Controls.Add(this.powerOptionsButton);
            this.powerSettingsTabPage.Controls.Add(this.unlockUltimatePerformanceButton);
            this.powerSettingsTabPage.Controls.Add(this.setPowerPlanButton);
            this.powerSettingsTabPage.Controls.Add(this.powerPlanComboBox);
            this.powerSettingsTabPage.Controls.Add(this.powerPlanLabel);
            this.powerSettingsTabPage.Location = new System.Drawing.Point(4, 22);
            this.powerSettingsTabPage.Name = "powerSettingsTabPage";
            this.powerSettingsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.powerSettingsTabPage.Size = new System.Drawing.Size(321, 252);
            this.powerSettingsTabPage.TabIndex = 1;
            this.powerSettingsTabPage.Text = "Power Settings";
            // 
            // powerOptionsButton
            // 
            this.powerOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.powerOptionsButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.powerOptionsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.powerOptionsButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.powerOptionsButton.Location = new System.Drawing.Point(99, 146);
            this.powerOptionsButton.Name = "powerOptionsButton";
            this.powerOptionsButton.Size = new System.Drawing.Size(120, 38);
            this.powerOptionsButton.TabIndex = 59;
            this.powerOptionsButton.Text = "Open Power Options";
            this.powerOptionsButton.UseVisualStyleBackColor = false;
            this.powerOptionsButton.Click += new System.EventHandler(this.PowerOptionsButton_Click);
            // 
            // unlockUltimatePerformanceButton
            // 
            this.unlockUltimatePerformanceButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.unlockUltimatePerformanceButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.unlockUltimatePerformanceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.unlockUltimatePerformanceButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.unlockUltimatePerformanceButton.Location = new System.Drawing.Point(176, 201);
            this.unlockUltimatePerformanceButton.Name = "unlockUltimatePerformanceButton";
            this.unlockUltimatePerformanceButton.Size = new System.Drawing.Size(120, 38);
            this.unlockUltimatePerformanceButton.TabIndex = 58;
            this.unlockUltimatePerformanceButton.Text = "Unlock \"Ultimate Performance\"";
            this.unlockUltimatePerformanceButton.UseVisualStyleBackColor = false;
            this.unlockUltimatePerformanceButton.Click += new System.EventHandler(this.UnlockUltimatePerformanceButton_Click);
            // 
            // setPowerPlanButton
            // 
            this.setPowerPlanButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.setPowerPlanButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.setPowerPlanButton.Enabled = false;
            this.setPowerPlanButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.setPowerPlanButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.setPowerPlanButton.Location = new System.Drawing.Point(19, 201);
            this.setPowerPlanButton.Name = "setPowerPlanButton";
            this.setPowerPlanButton.Size = new System.Drawing.Size(120, 38);
            this.setPowerPlanButton.TabIndex = 57;
            this.setPowerPlanButton.Text = "Set Power Plan";
            this.setPowerPlanButton.UseVisualStyleBackColor = false;
            this.setPowerPlanButton.Click += new System.EventHandler(this.SetPowerPlanButton_Click);
            // 
            // powerPlanComboBox
            // 
            this.powerPlanComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.powerPlanComboBox.FormattingEnabled = true;
            this.powerPlanComboBox.Location = new System.Drawing.Point(92, 11);
            this.powerPlanComboBox.Name = "powerPlanComboBox";
            this.powerPlanComboBox.Size = new System.Drawing.Size(121, 21);
            this.powerPlanComboBox.TabIndex = 21;
            // 
            // powerPlanLabel
            // 
            this.powerPlanLabel.AutoSize = true;
            this.powerPlanLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.powerPlanLabel.Location = new System.Drawing.Point(16, 14);
            this.powerPlanLabel.Name = "powerPlanLabel";
            this.powerPlanLabel.Size = new System.Drawing.Size(64, 13);
            this.powerPlanLabel.TabIndex = 1;
            this.powerPlanLabel.Text = "Power Plan:";
            // 
            // timerResolutionTabPage
            // 
            this.timerResolutionTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.timerResolutionTabPage.Controls.Add(this.minimumTimerResolutionTextBox);
            this.timerResolutionTabPage.Controls.Add(this.maximumTimerResolutionTextBox);
            this.timerResolutionTabPage.Controls.Add(this.maximumTimerResolutionLabel);
            this.timerResolutionTabPage.Controls.Add(this.currentTimerResolutionTextBox);
            this.timerResolutionTabPage.Controls.Add(this.defaultTimerResolutionButton);
            this.timerResolutionTabPage.Controls.Add(this.minimumTimerResolutionLabel);
            this.timerResolutionTabPage.Controls.Add(this.setTimerResolutionButton);
            this.timerResolutionTabPage.Controls.Add(this.currentTimerResolutionLabel);
            this.timerResolutionTabPage.Location = new System.Drawing.Point(4, 22);
            this.timerResolutionTabPage.Name = "timerResolutionTabPage";
            this.timerResolutionTabPage.Size = new System.Drawing.Size(321, 252);
            this.timerResolutionTabPage.TabIndex = 3;
            this.timerResolutionTabPage.Text = "Timer Resolution";
            // 
            // minimumTimerResolutionTextBox
            // 
            this.minimumTimerResolutionTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.minimumTimerResolutionTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.minimumTimerResolutionTextBox.Location = new System.Drawing.Point(147, 12);
            this.minimumTimerResolutionTextBox.Name = "minimumTimerResolutionTextBox";
            this.minimumTimerResolutionTextBox.ReadOnly = true;
            this.minimumTimerResolutionTextBox.Size = new System.Drawing.Size(102, 20);
            this.minimumTimerResolutionTextBox.TabIndex = 8;
            // 
            // maximumTimerResolutionTextBox
            // 
            this.maximumTimerResolutionTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.maximumTimerResolutionTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.maximumTimerResolutionTextBox.Location = new System.Drawing.Point(147, 46);
            this.maximumTimerResolutionTextBox.Name = "maximumTimerResolutionTextBox";
            this.maximumTimerResolutionTextBox.ReadOnly = true;
            this.maximumTimerResolutionTextBox.Size = new System.Drawing.Size(102, 20);
            this.maximumTimerResolutionTextBox.TabIndex = 7;
            // 
            // maximumTimerResolutionLabel
            // 
            this.maximumTimerResolutionLabel.AutoSize = true;
            this.maximumTimerResolutionLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.maximumTimerResolutionLabel.Location = new System.Drawing.Point(15, 48);
            this.maximumTimerResolutionLabel.Name = "maximumTimerResolutionLabel";
            this.maximumTimerResolutionLabel.Size = new System.Drawing.Size(110, 13);
            this.maximumTimerResolutionLabel.TabIndex = 6;
            this.maximumTimerResolutionLabel.Text = "Maximum Resolution: ";
            // 
            // SystemPerformanceManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.ClientSize = new System.Drawing.Size(357, 302);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "SystemPerformanceManagerForm";
            this.Text = "Sytem Performance Manager";
            this.tabControl1.ResumeLayout(false);
            this.compatibilityTabPage.ResumeLayout(false);
            this.compatibilityTabPage.PerformLayout();
            this.powerSettingsTabPage.ResumeLayout(false);
            this.powerSettingsTabPage.PerformLayout();
            this.timerResolutionTabPage.ResumeLayout(false);
            this.timerResolutionTabPage.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label minimumTimerResolutionLabel;
        private System.Windows.Forms.Button setTimerResolutionButton;
        private System.Windows.Forms.Button defaultTimerResolutionButton;
        private System.Windows.Forms.Label currentTimerResolutionLabel;
        private System.Windows.Forms.TextBox currentTimerResolutionTextBox;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage timerResolutionTabPage;
        private System.Windows.Forms.TabPage compatibilityTabPage;
        private System.Windows.Forms.TabPage powerSettingsTabPage;
        private System.Windows.Forms.Label maximumTimerResolutionLabel;
        private System.Windows.Forms.TextBox minimumTimerResolutionTextBox;
        private System.Windows.Forms.TextBox maximumTimerResolutionTextBox;
        private System.Windows.Forms.Label compatibilityModeLabel;
        private System.Windows.Forms.CheckBox HDPIiScalingCheckBox;
        private System.Windows.Forms.Label HDPIScalingLabel;
        private System.Windows.Forms.CheckBox runAsAdministratorCheckBox;
        private System.Windows.Forms.Label runAsAdministratorLabel;
        private System.Windows.Forms.CheckBox comatibilityModeCheckBox;
        private System.Windows.Forms.Button setPropertiesButton;
        private System.Windows.Forms.Label powerPlanLabel;
        private System.Windows.Forms.Button setPowerPlanButton;
        private System.Windows.Forms.ComboBox powerPlanComboBox;
        private System.Windows.Forms.Button unlockUltimatePerformanceButton;
        private System.Windows.Forms.CheckBox fullscreenOptimizationsCheckBox;
        private System.Windows.Forms.Label fullscreenOptimizationsLabel;
        private System.Windows.Forms.Button powerOptionsButton;
        private System.Windows.Forms.Button openDoWPropertiesButton;
    }
}