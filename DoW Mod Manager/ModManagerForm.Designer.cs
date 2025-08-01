namespace DoW_Mod_Manager
{
    partial class ModManagerForm
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
            this.currentDirTextBox = new System.Windows.Forms.TextBox();
            this.currentDirectoryLabel = new System.Windows.Forms.Label();
            this.installedModsLabel = new System.Windows.Forms.Label();
            this.requiredModsLabel = new System.Windows.Forms.Label();
            this.installedModsListBox = new System.Windows.Forms.ListBox();
            this.requiredModsList = new System.Windows.Forms.ListBox();
            this.startModButton = new System.Windows.Forms.Button();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.authorLabel = new System.Windows.Forms.Label();
            this.advancedStartOptionsLabel = new System.Windows.Forms.Label();
            this.devCheckBox = new System.Windows.Forms.CheckBox();
            this.nomoviesCheckBox = new System.Windows.Forms.CheckBox();
            this.highpolyCheckBox = new System.Windows.Forms.CheckBox();
            this.optimizationsCheckBox = new System.Windows.Forms.CheckBox();
            this.mergeButton = new System.Windows.Forms.Button();
            this.startVanillaButton = new System.Windows.Forms.Button();
            this.gameLAAStatusLabel = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.open_folder_button = new System.Windows.Forms.Button();
            this.toggleLAAButton = new System.Windows.Forms.Button();
            this.fileSystemWatcher1 = new System.IO.FileSystemWatcher();
            this.downloadButton = new System.Windows.Forms.Button();
            this.settingsButton = new System.Windows.Forms.Button();
            this.AboutkLabel = new System.Windows.Forms.LinkLabel();
            this.fixMissingModButton = new System.Windows.Forms.Button();
            this.checkForErrorsButton = new System.Windows.Forms.Button();
            this.noFogCheckbox = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.noprecachemodelsCheckBox = new System.Windows.Forms.CheckBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.fontStatusLabel = new System.Windows.Forms.Label();
            this.fontButton = new System.Windows.Forms.Button();
            this.cameraStatusLabel = new System.Windows.Forms.Label();
            this.cameraButton = new System.Windows.Forms.Button();
            this.DXVKStatusLabel = new System.Windows.Forms.Label();
            this.dxvkButton = new System.Windows.Forms.Button();
            this.GOGRadioButton = new System.Windows.Forms.RadioButton();
            this.SteamRadioButton = new System.Windows.Forms.RadioButton();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher1)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // currentDirTextBox
            // 
            this.currentDirTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.currentDirTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.currentDirTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.currentDirTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.currentDirTextBox.Location = new System.Drawing.Point(140, 0);
            this.currentDirTextBox.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.currentDirTextBox.Name = "currentDirTextBox";
            this.currentDirTextBox.ReadOnly = true;
            this.currentDirTextBox.Size = new System.Drawing.Size(758, 20);
            this.currentDirTextBox.TabIndex = 0;
            // 
            // currentDirectoryLabel
            // 
            this.currentDirectoryLabel.AutoSize = true;
            this.currentDirectoryLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.currentDirectoryLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.currentDirectoryLabel.Location = new System.Drawing.Point(0, 3);
            this.currentDirectoryLabel.Margin = new System.Windows.Forms.Padding(0, 3, 3, 0);
            this.currentDirectoryLabel.Name = "currentDirectoryLabel";
            this.currentDirectoryLabel.Size = new System.Drawing.Size(137, 21);
            this.currentDirectoryLabel.TabIndex = 1;
            this.currentDirectoryLabel.Text = "Your current game directory";
            // 
            // installedModsLabel
            // 
            this.installedModsLabel.AutoSize = true;
            this.installedModsLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.installedModsLabel.Location = new System.Drawing.Point(4, 3);
            this.installedModsLabel.Name = "installedModsLabel";
            this.installedModsLabel.Size = new System.Drawing.Size(119, 13);
            this.installedModsLabel.TabIndex = 2;
            this.installedModsLabel.Text = "Currently Installed Mods";
            // 
            // requiredModsLabel
            // 
            this.requiredModsLabel.AutoSize = true;
            this.requiredModsLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.requiredModsLabel.Location = new System.Drawing.Point(5, 3);
            this.requiredModsLabel.Margin = new System.Windows.Forms.Padding(10);
            this.requiredModsLabel.Name = "requiredModsLabel";
            this.requiredModsLabel.Size = new System.Drawing.Size(79, 13);
            this.requiredModsLabel.TabIndex = 3;
            this.requiredModsLabel.Text = "Required Mods";
            // 
            // installedModsListBox
            // 
            this.installedModsListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.installedModsListBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.installedModsListBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.installedModsListBox.FormattingEnabled = true;
            this.installedModsListBox.Location = new System.Drawing.Point(6, 23);
            this.installedModsListBox.Margin = new System.Windows.Forms.Padding(0);
            this.installedModsListBox.Name = "installedModsListBox";
            this.installedModsListBox.ScrollAlwaysVisible = true;
            this.installedModsListBox.Size = new System.Drawing.Size(447, 342);
            this.installedModsListBox.TabIndex = 4;
            this.installedModsListBox.SelectedIndexChanged += new System.EventHandler(this.InstalledModsList_SelectedIndexChanged);
            // 
            // requiredModsList
            // 
            this.requiredModsList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.requiredModsList.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.requiredModsList.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.requiredModsList.FormattingEnabled = true;
            this.requiredModsList.Location = new System.Drawing.Point(8, 23);
            this.requiredModsList.Margin = new System.Windows.Forms.Padding(0);
            this.requiredModsList.Name = "requiredModsList";
            this.requiredModsList.ScrollAlwaysVisible = true;
            this.requiredModsList.Size = new System.Drawing.Size(427, 342);
            this.requiredModsList.TabIndex = 5;
            this.requiredModsList.SelectedIndexChanged += new System.EventHandler(this.RequiredModsList_SelectedIndexChanged);
            // 
            // startModButton
            // 
            this.startModButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.startModButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.startModButton.Enabled = false;
            this.startModButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startModButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.startModButton.Location = new System.Drawing.Point(130, 3);
            this.startModButton.Name = "startModButton";
            this.startModButton.Size = new System.Drawing.Size(121, 48);
            this.startModButton.TabIndex = 6;
            this.startModButton.Text = "START MOD";
            this.startModButton.UseVisualStyleBackColor = false;
            this.startModButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // pictureBox
            // 
            this.pictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox.ErrorImage = null;
            this.pictureBox.InitialImage = null;
            this.pictureBox.Location = new System.Drawing.Point(257, 3);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(50, 50);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox.TabIndex = 7;
            this.pictureBox.TabStop = false;
            // 
            // authorLabel
            // 
            this.authorLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.authorLabel.AutoSize = true;
            this.authorLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.authorLabel.Location = new System.Drawing.Point(16, 596);
            this.authorLabel.Name = "authorLabel";
            this.authorLabel.Size = new System.Drawing.Size(198, 13);
            this.authorLabel.TabIndex = 8;
            this.authorLabel.Text = "Created by FragJacker and IgorTheLight";
            // 
            // advancedStartOptionsLabel
            // 
            this.advancedStartOptionsLabel.AutoSize = true;
            this.advancedStartOptionsLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.advancedStartOptionsLabel.Location = new System.Drawing.Point(0, 0);
            this.advancedStartOptionsLabel.Margin = new System.Windows.Forms.Padding(0);
            this.advancedStartOptionsLabel.Name = "advancedStartOptionsLabel";
            this.advancedStartOptionsLabel.Size = new System.Drawing.Size(123, 13);
            this.advancedStartOptionsLabel.TabIndex = 9;
            this.advancedStartOptionsLabel.Text = "Advanced Start Options:";
            // 
            // devCheckBox
            // 
            this.devCheckBox.AutoSize = true;
            this.devCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.devCheckBox.Location = new System.Drawing.Point(5, 18);
            this.devCheckBox.Margin = new System.Windows.Forms.Padding(5, 5, 0, 0);
            this.devCheckBox.Name = "devCheckBox";
            this.devCheckBox.Size = new System.Drawing.Size(136, 17);
            this.devCheckBox.TabIndex = 10;
            this.devCheckBox.Text = "-dev: Developers mode";
            this.devCheckBox.UseVisualStyleBackColor = true;
            // 
            // nomoviesCheckBox
            // 
            this.nomoviesCheckBox.AutoSize = true;
            this.nomoviesCheckBox.Checked = true;
            this.nomoviesCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.nomoviesCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.nomoviesCheckBox.Location = new System.Drawing.Point(5, 35);
            this.nomoviesCheckBox.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.nomoviesCheckBox.Name = "nomoviesCheckBox";
            this.nomoviesCheckBox.Size = new System.Drawing.Size(154, 17);
            this.nomoviesCheckBox.TabIndex = 11;
            this.nomoviesCheckBox.Text = "-nomovies: No Intro movies";
            this.nomoviesCheckBox.UseVisualStyleBackColor = true;
            // 
            // highpolyCheckBox
            // 
            this.highpolyCheckBox.AutoSize = true;
            this.highpolyCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.highpolyCheckBox.Location = new System.Drawing.Point(5, 52);
            this.highpolyCheckBox.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.highpolyCheckBox.Name = "highpolyCheckBox";
            this.highpolyCheckBox.Size = new System.Drawing.Size(254, 17);
            this.highpolyCheckBox.TabIndex = 12;
            this.highpolyCheckBox.Text = "-forcehighpoly: High Poly models at any distance";
            this.highpolyCheckBox.UseVisualStyleBackColor = true;
            // 
            // optimizationsCheckBox
            // 
            this.optimizationsCheckBox.AutoSize = true;
            this.optimizationsCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.optimizationsCheckBox.Location = new System.Drawing.Point(5, 86);
            this.optimizationsCheckBox.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.optimizationsCheckBox.Name = "optimizationsCheckBox";
            this.optimizationsCheckBox.Size = new System.Drawing.Size(314, 17);
            this.optimizationsCheckBox.TabIndex = 13;
            this.optimizationsCheckBox.Text = "/high /affinity 6: Set to highest thread priority and CPU affinity";
            this.optimizationsCheckBox.UseVisualStyleBackColor = true;
            // 
            // mergeButton
            // 
            this.mergeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.mergeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.mergeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.mergeButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.mergeButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.mergeButton.Location = new System.Drawing.Point(130, 59);
            this.mergeButton.Name = "mergeButton";
            this.mergeButton.Size = new System.Drawing.Size(121, 43);
            this.mergeButton.TabIndex = 14;
            this.mergeButton.Text = "Merge Mods...";
            this.mergeButton.UseVisualStyleBackColor = false;
            this.mergeButton.Click += new System.EventHandler(this.ModMergeButton_Click);
            // 
            // startVanillaButton
            // 
            this.startVanillaButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.startVanillaButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.startVanillaButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.startVanillaButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.startVanillaButton.Location = new System.Drawing.Point(3, 3);
            this.startVanillaButton.Name = "startVanillaButton";
            this.startVanillaButton.Size = new System.Drawing.Size(121, 48);
            this.startVanillaButton.TabIndex = 15;
            this.startVanillaButton.Text = "START BASE GAME";
            this.startVanillaButton.UseVisualStyleBackColor = false;
            this.startVanillaButton.Click += new System.EventHandler(this.StartVanillaGameButton_Click);
            // 
            // gameLAAStatusLabel
            // 
            this.gameLAAStatusLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.gameLAAStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gameLAAStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this.gameLAAStatusLabel.Location = new System.Drawing.Point(182, 47);
            this.gameLAAStatusLabel.Margin = new System.Windows.Forms.Padding(3);
            this.gameLAAStatusLabel.Name = "gameLAAStatusLabel";
            this.gameLAAStatusLabel.Size = new System.Drawing.Size(65, 13);
            this.gameLAAStatusLabel.TabIndex = 16;
            this.gameLAAStatusLabel.Text = "Enabled";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.splitContainer1);
            this.panel1.Location = new System.Drawing.Point(12, 220);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(901, 377);
            this.panel1.TabIndex = 18;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 3);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.open_folder_button);
            this.splitContainer1.Panel1.Controls.Add(this.installedModsLabel);
            this.splitContainer1.Panel1.Controls.Add(this.installedModsListBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.requiredModsLabel);
            this.splitContainer1.Panel2.Controls.Add(this.requiredModsList);
            this.splitContainer1.Size = new System.Drawing.Size(898, 385);
            this.splitContainer1.SplitterDistance = 455;
            this.splitContainer1.TabIndex = 19;
            // 
            // open_folder_button
            // 
            this.open_folder_button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.open_folder_button.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.open_folder_button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.open_folder_button.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.open_folder_button.Location = new System.Drawing.Point(362, 0);
            this.open_folder_button.Name = "open_folder_button";
            this.open_folder_button.Size = new System.Drawing.Size(91, 23);
            this.open_folder_button.TabIndex = 5;
            this.open_folder_button.Text = "Open Folder...";
            this.open_folder_button.UseVisualStyleBackColor = false;
            this.open_folder_button.Click += new System.EventHandler(this.OpenFolderButton_Click);
            // 
            // toggleLAAButton
            // 
            this.toggleLAAButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.toggleLAAButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.toggleLAAButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.toggleLAAButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.toggleLAAButton.Location = new System.Drawing.Point(11, 40);
            this.toggleLAAButton.Name = "toggleLAAButton";
            this.toggleLAAButton.Size = new System.Drawing.Size(154, 31);
            this.toggleLAAButton.TabIndex = 20;
            this.toggleLAAButton.Text = "Toggle 4Gb RAM patch";
            this.toggleLAAButton.UseVisualStyleBackColor = false;
            this.toggleLAAButton.Click += new System.EventHandler(this.ButtonToggleLAA_Click);
            // 
            // fileSystemWatcher1
            // 
            this.fileSystemWatcher1.EnableRaisingEvents = true;
            this.fileSystemWatcher1.SynchronizingObject = this;
            // 
            // downloadButton
            // 
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.downloadButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.downloadButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.downloadButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.downloadButton.Location = new System.Drawing.Point(3, 59);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(121, 43);
            this.downloadButton.TabIndex = 21;
            this.downloadButton.Text = "Download Mod...";
            this.downloadButton.UseVisualStyleBackColor = false;
            this.downloadButton.Click += new System.EventHandler(this.DownloadButton_Click);
            // 
            // settingsButton
            // 
            this.settingsButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.settingsButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.settingsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.settingsButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.settingsButton.Location = new System.Drawing.Point(11, 3);
            this.settingsButton.Name = "settingsButton";
            this.settingsButton.Size = new System.Drawing.Size(104, 31);
            this.settingsButton.TabIndex = 22;
            this.settingsButton.Text = "SETTINGS";
            this.settingsButton.UseVisualStyleBackColor = false;
            this.settingsButton.Click += new System.EventHandler(this.SettingsButton_Click);
            // 
            // AboutkLabel
            // 
            this.AboutkLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.AboutkLabel.AutoSize = true;
            this.AboutkLabel.LinkColor = System.Drawing.Color.DodgerBlue;
            this.AboutkLabel.Location = new System.Drawing.Point(818, 596);
            this.AboutkLabel.Name = "AboutkLabel";
            this.AboutkLabel.Size = new System.Drawing.Size(99, 13);
            this.AboutkLabel.TabIndex = 23;
            this.AboutkLabel.TabStop = true;
            this.AboutkLabel.Text = "About and Updates";
            this.AboutkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.HomePageLinkLabel_LinkClicked);
            // 
            // fixMissingModButton
            // 
            this.fixMissingModButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.fixMissingModButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.fixMissingModButton.Enabled = false;
            this.fixMissingModButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.fixMissingModButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.fixMissingModButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.fixMissingModButton.Location = new System.Drawing.Point(257, 59);
            this.fixMissingModButton.Name = "fixMissingModButton";
            this.fixMissingModButton.Size = new System.Drawing.Size(59, 43);
            this.fixMissingModButton.TabIndex = 24;
            this.fixMissingModButton.Text = "Find MISSING";
            this.fixMissingModButton.UseVisualStyleBackColor = false;
            this.fixMissingModButton.Click += new System.EventHandler(this.FixMissingModButton_Click);
            // 
            // checkForErrorsButton
            // 
            this.checkForErrorsButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.checkForErrorsButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.checkForErrorsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkForErrorsButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.checkForErrorsButton.Location = new System.Drawing.Point(121, 3);
            this.checkForErrorsButton.Name = "checkForErrorsButton";
            this.checkForErrorsButton.Size = new System.Drawing.Size(104, 31);
            this.checkForErrorsButton.TabIndex = 25;
            this.checkForErrorsButton.Text = "Check for errors";
            this.checkForErrorsButton.UseVisualStyleBackColor = false;
            this.checkForErrorsButton.Click += new System.EventHandler(this.CheckForErrorsButton_Click);
            // 
            // noFogCheckbox
            // 
            this.noFogCheckbox.AutoSize = true;
            this.noFogCheckbox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.noFogCheckbox.Location = new System.Drawing.Point(5, 103);
            this.noFogCheckbox.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.noFogCheckbox.Name = "noFogCheckbox";
            this.noFogCheckbox.Size = new System.Drawing.Size(235, 17);
            this.noFogCheckbox.TabIndex = 27;
            this.noFogCheckbox.Text = "Disable Fog: Removes the map ambient fog.";
            this.noFogCheckbox.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel1.Controls.Add(this.advancedStartOptionsLabel);
            this.flowLayoutPanel1.Controls.Add(this.devCheckBox);
            this.flowLayoutPanel1.Controls.Add(this.nomoviesCheckBox);
            this.flowLayoutPanel1.Controls.Add(this.highpolyCheckBox);
            this.flowLayoutPanel1.Controls.Add(this.noprecachemodelsCheckBox);
            this.flowLayoutPanel1.Controls.Add(this.optimizationsCheckBox);
            this.flowLayoutPanel1.Controls.Add(this.noFogCheckbox);
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 32);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(328, 185);
            this.flowLayoutPanel1.TabIndex = 28;
            // 
            // noprecachemodelsCheckBox
            // 
            this.noprecachemodelsCheckBox.AutoSize = true;
            this.noprecachemodelsCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.noprecachemodelsCheckBox.Location = new System.Drawing.Point(5, 69);
            this.noprecachemodelsCheckBox.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.noprecachemodelsCheckBox.Name = "noprecachemodelsCheckBox";
            this.noprecachemodelsCheckBox.Size = new System.Drawing.Size(237, 17);
            this.noprecachemodelsCheckBox.TabIndex = 28;
            this.noprecachemodelsCheckBox.Text = "-noprecachemodels:  Don\'t precache models";
            this.noprecachemodelsCheckBox.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.fontStatusLabel);
            this.panel2.Controls.Add(this.fontButton);
            this.panel2.Controls.Add(this.cameraStatusLabel);
            this.panel2.Controls.Add(this.cameraButton);
            this.panel2.Controls.Add(this.DXVKStatusLabel);
            this.panel2.Controls.Add(this.toggleLAAButton);
            this.panel2.Controls.Add(this.checkForErrorsButton);
            this.panel2.Controls.Add(this.gameLAAStatusLabel);
            this.panel2.Controls.Add(this.settingsButton);
            this.panel2.Controls.Add(this.dxvkButton);
            this.panel2.Location = new System.Drawing.Point(341, 32);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(245, 185);
            this.panel2.TabIndex = 29;
            // 
            // fontStatusLabel
            // 
            this.fontStatusLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.fontStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.fontStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this.fontStatusLabel.Location = new System.Drawing.Point(182, 159);
            this.fontStatusLabel.Margin = new System.Windows.Forms.Padding(3);
            this.fontStatusLabel.Name = "fontStatusLabel";
            this.fontStatusLabel.Size = new System.Drawing.Size(65, 20);
            this.fontStatusLabel.TabIndex = 30;
            this.fontStatusLabel.Text = "Enabled";
            // 
            // fontButton
            // 
            this.fontButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.fontButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.fontButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.fontButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.fontButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.fontButton.Location = new System.Drawing.Point(11, 151);
            this.fontButton.Name = "fontButton";
            this.fontButton.Size = new System.Drawing.Size(154, 31);
            this.fontButton.TabIndex = 29;
            this.fontButton.Text = "Install larger fonts";
            this.fontButton.UseVisualStyleBackColor = false;
            this.fontButton.Click += new System.EventHandler(this.FontButton_Click);
            // 
            // cameraStatusLabel
            // 
            this.cameraStatusLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.cameraStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cameraStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this.cameraStatusLabel.Location = new System.Drawing.Point(182, 122);
            this.cameraStatusLabel.Margin = new System.Windows.Forms.Padding(3);
            this.cameraStatusLabel.Name = "cameraStatusLabel";
            this.cameraStatusLabel.Size = new System.Drawing.Size(65, 20);
            this.cameraStatusLabel.TabIndex = 28;
            this.cameraStatusLabel.Text = "Enabled";
            // 
            // cameraButton
            // 
            this.cameraButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cameraButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.cameraButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cameraButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.cameraButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.cameraButton.Location = new System.Drawing.Point(11, 114);
            this.cameraButton.Name = "cameraButton";
            this.cameraButton.Size = new System.Drawing.Size(154, 31);
            this.cameraButton.TabIndex = 27;
            this.cameraButton.Text = "Install a better camera";
            this.cameraButton.UseVisualStyleBackColor = false;
            this.cameraButton.Click += new System.EventHandler(this.CameraButton_Click);
            // 
            // DXVKStatusLabel
            // 
            this.DXVKStatusLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.DXVKStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DXVKStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this.DXVKStatusLabel.Location = new System.Drawing.Point(182, 85);
            this.DXVKStatusLabel.Margin = new System.Windows.Forms.Padding(3);
            this.DXVKStatusLabel.Name = "DXVKStatusLabel";
            this.DXVKStatusLabel.Size = new System.Drawing.Size(65, 15);
            this.DXVKStatusLabel.TabIndex = 26;
            this.DXVKStatusLabel.Text = "Enabled";
            // 
            // dxvkButton
            // 
            this.dxvkButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dxvkButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.dxvkButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.dxvkButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.dxvkButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.dxvkButton.Location = new System.Drawing.Point(11, 77);
            this.dxvkButton.Name = "dxvkButton";
            this.dxvkButton.Size = new System.Drawing.Size(154, 31);
            this.dxvkButton.TabIndex = 25;
            this.dxvkButton.Text = "Install DXVK";
            this.dxvkButton.UseVisualStyleBackColor = false;
            this.dxvkButton.Click += new System.EventHandler(this.DxvkButton_Click);
            // 
            // GOGRadioButton
            // 
            this.GOGRadioButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.GOGRadioButton.AutoSize = true;
            this.GOGRadioButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.GOGRadioButton.Location = new System.Drawing.Point(101, 108);
            this.GOGRadioButton.Name = "GOGRadioButton";
            this.GOGRadioButton.Size = new System.Drawing.Size(86, 17);
            this.GOGRadioButton.TabIndex = 27;
            this.GOGRadioButton.Text = "GOG version";
            this.GOGRadioButton.UseVisualStyleBackColor = true;
            this.GOGRadioButton.CheckedChanged += new System.EventHandler(this.GOGRadioButton_CheckedChanged);
            // 
            // SteamRadioButton
            // 
            this.SteamRadioButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.SteamRadioButton.AutoSize = true;
            this.SteamRadioButton.Checked = true;
            this.SteamRadioButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.SteamRadioButton.Location = new System.Drawing.Point(3, 108);
            this.SteamRadioButton.Name = "SteamRadioButton";
            this.SteamRadioButton.Size = new System.Drawing.Size(92, 17);
            this.SteamRadioButton.TabIndex = 26;
            this.SteamRadioButton.TabStop = true;
            this.SteamRadioButton.Text = "Steam version";
            this.SteamRadioButton.UseVisualStyleBackColor = true;
            this.SteamRadioButton.CheckedChanged += new System.EventHandler(this.SteamRadioButton_CheckedChanged);
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel2.Controls.Add(this.startVanillaButton);
            this.flowLayoutPanel2.Controls.Add(this.startModButton);
            this.flowLayoutPanel2.Controls.Add(this.pictureBox);
            this.flowLayoutPanel2.Controls.Add(this.downloadButton);
            this.flowLayoutPanel2.Controls.Add(this.mergeButton);
            this.flowLayoutPanel2.Controls.Add(this.fixMissingModButton);
            this.flowLayoutPanel2.Controls.Add(this.SteamRadioButton);
            this.flowLayoutPanel2.Controls.Add(this.GOGRadioButton);
            this.flowLayoutPanel2.Location = new System.Drawing.Point(592, 32);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(321, 185);
            this.flowLayoutPanel2.TabIndex = 30;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.currentDirTextBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.currentDirectoryLabel, 0, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(901, 24);
            this.tableLayoutPanel1.TabIndex = 31;
            // 
            // ModManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.ClientSize = new System.Drawing.Size(925, 614);
            this.Controls.Add(this.flowLayoutPanel2);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.AboutkLabel);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.authorLabel);
            this.MinimumSize = new System.Drawing.Size(941, 653);
            this.Name = "ModManagerForm";
            this.Text = "DoW Mod Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ModManagerForm_Closing);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.ModManagerForm_MouseMove);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher1)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox currentDirTextBox;
        private System.Windows.Forms.Label currentDirectoryLabel;
        private System.Windows.Forms.Label installedModsLabel;
        private System.Windows.Forms.Label requiredModsLabel;
        private System.Windows.Forms.ListBox requiredModsList;
        private System.Windows.Forms.Button startModButton;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label authorLabel;
        private System.Windows.Forms.Label advancedStartOptionsLabel;
        private System.Windows.Forms.CheckBox devCheckBox;
        private System.Windows.Forms.CheckBox nomoviesCheckBox;
        private System.Windows.Forms.CheckBox highpolyCheckBox;
        private System.Windows.Forms.CheckBox optimizationsCheckBox;
        private System.Windows.Forms.Button mergeButton;
        private System.Windows.Forms.ListBox installedModsListBox;
        private System.Windows.Forms.Button startVanillaButton;
        private System.Windows.Forms.Label gameLAAStatusLabel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button toggleLAAButton;
        private System.IO.FileSystemWatcher fileSystemWatcher1;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.Button settingsButton;
        private System.Windows.Forms.LinkLabel AboutkLabel;
        private System.Windows.Forms.Button fixMissingModButton;
        private System.Windows.Forms.Button checkForErrorsButton;
        private System.Windows.Forms.CheckBox noFogCheckbox;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button open_folder_button;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.RadioButton GOGRadioButton;
        private System.Windows.Forms.RadioButton SteamRadioButton;
        private System.Windows.Forms.CheckBox noprecachemodelsCheckBox;
        private System.Windows.Forms.Button dxvkButton;
        private System.Windows.Forms.Label DXVKStatusLabel;
        private System.Windows.Forms.Button cameraButton;
        private System.Windows.Forms.Label cameraStatusLabel;
        private System.Windows.Forms.Label fontStatusLabel;
        private System.Windows.Forms.Button fontButton;
    }
}