namespace UDP_Patcher
{
    partial class Form1
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.pictureBox3 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonSelectUnpatchedGDI = new System.Windows.Forms.Button();
            this.patchBuildProgressDetails = new System.Windows.Forms.Label();
            this.patchBuildProgressPercentage = new System.Windows.Forms.Label();
            this.patchBuildProgressBar = new System.Windows.Forms.ProgressBar();
            this.buttonQuit = new System.Windows.Forms.Button();
            this.buttonSelectPatchedGDI = new System.Windows.Forms.Button();
            this.buttonBuildPatch = new System.Windows.Forms.Button();
            this.textboxPatchFilename = new System.Windows.Forms.TextBox();
            this.patchFileExtensionLabel = new System.Windows.Forms.Label();
            this.patchFilenameLabel = new System.Windows.Forms.Label();
            this.groupboxStep1 = new System.Windows.Forms.GroupBox();
            this.groupboxStep2 = new System.Windows.Forms.GroupBox();
            this.groupboxStep3 = new System.Windows.Forms.GroupBox();
            this.ipbinTree5 = new System.Windows.Forms.Label();
            this.ipbinTree4 = new System.Windows.Forms.Label();
            this.textboxGameNameIPBIN = new System.Windows.Forms.TextBox();
            this.checkboxCustomNameIPBIN = new System.Windows.Forms.CheckBox();
            this.ipbinTree3 = new System.Windows.Forms.Label();
            this.ipbinTree2 = new System.Windows.Forms.Label();
            this.ipbinTree1 = new System.Windows.Forms.Label();
            this.checkboxVGAIPBIN = new System.Windows.Forms.CheckBox();
            this.checkboxRegionFreeIPBIN = new System.Windows.Forms.CheckBox();
            this.dropdownPatchedIPBINSource = new System.Windows.Forms.ComboBox();
            this.checkboxAdditionalPatchingIPBIN = new System.Windows.Forms.CheckBox();
            this.checkboxUsePatchedIPBIN = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.groupboxStep1.SuspendLayout();
            this.groupboxStep2.SuspendLayout();
            this.groupboxStep3.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.InitialImage = null;
            this.pictureBox1.Location = new System.Drawing.Point(8, 8);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(424, 120);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 5;
            this.pictureBox1.TabStop = false;
            // 
            // pictureBox3
            // 
            this.pictureBox3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pictureBox3.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox3.Image")));
            this.pictureBox3.InitialImage = null;
            this.pictureBox3.Location = new System.Drawing.Point(392, 664);
            this.pictureBox3.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new System.Drawing.Size(22, 26);
            this.pictureBox3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox3.TabIndex = 14;
            this.pictureBox3.TabStop = false;
            this.pictureBox3.Visible = false;
            // 
            // pictureBox2
            // 
            this.pictureBox2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
            this.pictureBox2.InitialImage = null;
            this.pictureBox2.Location = new System.Drawing.Point(416, 664);
            this.pictureBox2.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(19, 20);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox2.TabIndex = 13;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Visible = false;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(8, 664);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(424, 24);
            this.label2.TabIndex = 15;
            this.label2.Text = "v1.5 - Derek Pascarella (ateam)";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // buttonSelectUnpatchedGDI
            // 
            this.buttonSelectUnpatchedGDI.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSelectUnpatchedGDI.Location = new System.Drawing.Point(16, 32);
            this.buttonSelectUnpatchedGDI.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSelectUnpatchedGDI.Name = "buttonSelectUnpatchedGDI";
            this.buttonSelectUnpatchedGDI.Size = new System.Drawing.Size(192, 40);
            this.buttonSelectUnpatchedGDI.TabIndex = 17;
            this.buttonSelectUnpatchedGDI.Text = "Select Original GDI";
            this.buttonSelectUnpatchedGDI.UseVisualStyleBackColor = true;
            this.buttonSelectUnpatchedGDI.Click += new System.EventHandler(this.ButtonSelectUnpatchedGDI_Click);
            // 
            // patchBuildProgressDetails
            // 
            this.patchBuildProgressDetails.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.patchBuildProgressDetails.Location = new System.Drawing.Point(8, 560);
            this.patchBuildProgressDetails.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.patchBuildProgressDetails.Name = "patchBuildProgressDetails";
            this.patchBuildProgressDetails.Size = new System.Drawing.Size(424, 24);
            this.patchBuildProgressDetails.TabIndex = 20;
            this.patchBuildProgressDetails.Text = "Starting patch-building process...";
            this.patchBuildProgressDetails.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.patchBuildProgressDetails.Visible = false;
            // 
            // patchBuildProgressPercentage
            // 
            this.patchBuildProgressPercentage.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.patchBuildProgressPercentage.Location = new System.Drawing.Point(8, 584);
            this.patchBuildProgressPercentage.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.patchBuildProgressPercentage.Name = "patchBuildProgressPercentage";
            this.patchBuildProgressPercentage.Size = new System.Drawing.Size(424, 24);
            this.patchBuildProgressPercentage.TabIndex = 22;
            this.patchBuildProgressPercentage.Text = "0%";
            this.patchBuildProgressPercentage.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.patchBuildProgressPercentage.Visible = false;
            // 
            // patchBuildProgressBar
            // 
            this.patchBuildProgressBar.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            this.patchBuildProgressBar.Location = new System.Drawing.Point(8, 616);
            this.patchBuildProgressBar.Margin = new System.Windows.Forms.Padding(2);
            this.patchBuildProgressBar.MarqueeAnimationSpeed = 0;
            this.patchBuildProgressBar.Name = "patchBuildProgressBar";
            this.patchBuildProgressBar.Size = new System.Drawing.Size(424, 32);
            this.patchBuildProgressBar.TabIndex = 19;
            this.patchBuildProgressBar.UseWaitCursor = true;
            this.patchBuildProgressBar.Visible = false;
            // 
            // buttonQuit
            // 
            this.buttonQuit.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonQuit.Location = new System.Drawing.Point(8, 608);
            this.buttonQuit.Margin = new System.Windows.Forms.Padding(2);
            this.buttonQuit.Name = "buttonQuit";
            this.buttonQuit.Size = new System.Drawing.Size(424, 40);
            this.buttonQuit.TabIndex = 18;
            this.buttonQuit.Text = "Quit";
            this.buttonQuit.UseVisualStyleBackColor = true;
            this.buttonQuit.Click += new System.EventHandler(this.ButtonQuit_Click);
            // 
            // buttonSelectPatchedGDI
            // 
            this.buttonSelectPatchedGDI.Enabled = false;
            this.buttonSelectPatchedGDI.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSelectPatchedGDI.Location = new System.Drawing.Point(216, 32);
            this.buttonSelectPatchedGDI.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSelectPatchedGDI.Name = "buttonSelectPatchedGDI";
            this.buttonSelectPatchedGDI.Size = new System.Drawing.Size(192, 40);
            this.buttonSelectPatchedGDI.TabIndex = 21;
            this.buttonSelectPatchedGDI.Text = "Select Modified GDI";
            this.buttonSelectPatchedGDI.UseVisualStyleBackColor = true;
            this.buttonSelectPatchedGDI.Click += new System.EventHandler(this.ButtonSelectPatchedGDI_Click);
            // 
            // buttonBuildPatch
            // 
            this.buttonBuildPatch.Enabled = false;
            this.buttonBuildPatch.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonBuildPatch.Location = new System.Drawing.Point(8, 560);
            this.buttonBuildPatch.Margin = new System.Windows.Forms.Padding(2);
            this.buttonBuildPatch.Name = "buttonBuildPatch";
            this.buttonBuildPatch.Size = new System.Drawing.Size(424, 40);
            this.buttonBuildPatch.TabIndex = 16;
            this.buttonBuildPatch.Text = "Build Patch";
            this.buttonBuildPatch.UseVisualStyleBackColor = true;
            this.buttonBuildPatch.Click += new System.EventHandler(this.ButtonBuildPatch_Click);
            // 
            // textboxPatchFilename
            // 
            this.textboxPatchFilename.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.textboxPatchFilename.Location = new System.Drawing.Point(136, 32);
            this.textboxPatchFilename.MaxLength = 255;
            this.textboxPatchFilename.Name = "textboxPatchFilename";
            this.textboxPatchFilename.Size = new System.Drawing.Size(232, 26);
            this.textboxPatchFilename.TabIndex = 23;
            this.textboxPatchFilename.Text = "My Game Name (English v1.0)";
            // 
            // patchFileExtensionLabel
            // 
            this.patchFileExtensionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.patchFileExtensionLabel.Location = new System.Drawing.Point(352, 32);
            this.patchFileExtensionLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.patchFileExtensionLabel.Name = "patchFileExtensionLabel";
            this.patchFileExtensionLabel.Size = new System.Drawing.Size(56, 24);
            this.patchFileExtensionLabel.TabIndex = 24;
            this.patchFileExtensionLabel.Text = ".dcp";
            this.patchFileExtensionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // patchFilenameLabel
            // 
            this.patchFilenameLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.patchFilenameLabel.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.patchFilenameLabel.Location = new System.Drawing.Point(16, 32);
            this.patchFilenameLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.patchFilenameLabel.Name = "patchFilenameLabel";
            this.patchFilenameLabel.Size = new System.Drawing.Size(392, 24);
            this.patchFilenameLabel.TabIndex = 25;
            this.patchFilenameLabel.Text = "Patch filename:";
            this.patchFilenameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupboxStep1
            // 
            this.groupboxStep1.Controls.Add(this.buttonSelectUnpatchedGDI);
            this.groupboxStep1.Controls.Add(this.buttonSelectPatchedGDI);
            this.groupboxStep1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupboxStep1.Location = new System.Drawing.Point(8, 136);
            this.groupboxStep1.Name = "groupboxStep1";
            this.groupboxStep1.Size = new System.Drawing.Size(424, 88);
            this.groupboxStep1.TabIndex = 26;
            this.groupboxStep1.TabStop = false;
            this.groupboxStep1.Text = "Step 1";
            // 
            // groupboxStep2
            // 
            this.groupboxStep2.Controls.Add(this.textboxPatchFilename);
            this.groupboxStep2.Controls.Add(this.patchFileExtensionLabel);
            this.groupboxStep2.Controls.Add(this.patchFilenameLabel);
            this.groupboxStep2.Enabled = false;
            this.groupboxStep2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupboxStep2.Location = new System.Drawing.Point(8, 232);
            this.groupboxStep2.Name = "groupboxStep2";
            this.groupboxStep2.Size = new System.Drawing.Size(424, 72);
            this.groupboxStep2.TabIndex = 27;
            this.groupboxStep2.TabStop = false;
            this.groupboxStep2.Text = "Step 2";
            // 
            // groupboxStep3
            // 
            this.groupboxStep3.Controls.Add(this.ipbinTree5);
            this.groupboxStep3.Controls.Add(this.ipbinTree4);
            this.groupboxStep3.Controls.Add(this.textboxGameNameIPBIN);
            this.groupboxStep3.Controls.Add(this.checkboxCustomNameIPBIN);
            this.groupboxStep3.Controls.Add(this.ipbinTree3);
            this.groupboxStep3.Controls.Add(this.ipbinTree2);
            this.groupboxStep3.Controls.Add(this.ipbinTree1);
            this.groupboxStep3.Controls.Add(this.checkboxVGAIPBIN);
            this.groupboxStep3.Controls.Add(this.checkboxRegionFreeIPBIN);
            this.groupboxStep3.Controls.Add(this.dropdownPatchedIPBINSource);
            this.groupboxStep3.Controls.Add(this.checkboxAdditionalPatchingIPBIN);
            this.groupboxStep3.Controls.Add(this.checkboxUsePatchedIPBIN);
            this.groupboxStep3.Enabled = false;
            this.groupboxStep3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupboxStep3.Location = new System.Drawing.Point(8, 312);
            this.groupboxStep3.Name = "groupboxStep3";
            this.groupboxStep3.Size = new System.Drawing.Size(424, 232);
            this.groupboxStep3.TabIndex = 28;
            this.groupboxStep3.TabStop = false;
            this.groupboxStep3.Text = "Step 3";
            // 
            // ipbinTree5
            // 
            this.ipbinTree5.BackColor = System.Drawing.Color.Silver;
            this.ipbinTree5.ForeColor = System.Drawing.Color.Black;
            this.ipbinTree5.Location = new System.Drawing.Point(24, 200);
            this.ipbinTree5.Name = "ipbinTree5";
            this.ipbinTree5.Size = new System.Drawing.Size(16, 1);
            this.ipbinTree5.TabIndex = 37;
            this.ipbinTree5.Text = "dd\r\ndd\r\ndd";
            // 
            // ipbinTree4
            // 
            this.ipbinTree4.BackColor = System.Drawing.Color.Silver;
            this.ipbinTree4.ForeColor = System.Drawing.Color.Black;
            this.ipbinTree4.Location = new System.Drawing.Point(24, 184);
            this.ipbinTree4.Name = "ipbinTree4";
            this.ipbinTree4.Size = new System.Drawing.Size(1, 17);
            this.ipbinTree4.TabIndex = 36;
            this.ipbinTree4.Text = "dd\r\ndd\r\ndd";
            // 
            // textboxGameNameIPBIN
            // 
            this.textboxGameNameIPBIN.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textboxGameNameIPBIN.Enabled = false;
            this.textboxGameNameIPBIN.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.textboxGameNameIPBIN.Location = new System.Drawing.Point(48, 192);
            this.textboxGameNameIPBIN.MaxLength = 128;
            this.textboxGameNameIPBIN.Name = "textboxGameNameIPBIN";
            this.textboxGameNameIPBIN.Size = new System.Drawing.Size(360, 26);
            this.textboxGameNameIPBIN.TabIndex = 26;
            this.textboxGameNameIPBIN.Text = "MY GAME NAME (ENGLISH)";
            // 
            // checkboxCustomNameIPBIN
            // 
            this.checkboxCustomNameIPBIN.AutoSize = true;
            this.checkboxCustomNameIPBIN.Enabled = false;
            this.checkboxCustomNameIPBIN.Location = new System.Drawing.Point(16, 160);
            this.checkboxCustomNameIPBIN.Name = "checkboxCustomNameIPBIN";
            this.checkboxCustomNameIPBIN.Size = new System.Drawing.Size(271, 24);
            this.checkboxCustomNameIPBIN.TabIndex = 35;
            this.checkboxCustomNameIPBIN.Text = "Use custom game name in IP.BIN.";
            this.checkboxCustomNameIPBIN.UseVisualStyleBackColor = true;
            this.checkboxCustomNameIPBIN.CheckedChanged += new System.EventHandler(this.CheckboxCustomNameIPBIN_CheckedChanged);
            // 
            // ipbinTree3
            // 
            this.ipbinTree3.BackColor = System.Drawing.Color.Silver;
            this.ipbinTree3.ForeColor = System.Drawing.Color.Black;
            this.ipbinTree3.Location = new System.Drawing.Point(24, 136);
            this.ipbinTree3.Name = "ipbinTree3";
            this.ipbinTree3.Size = new System.Drawing.Size(16, 1);
            this.ipbinTree3.TabIndex = 34;
            this.ipbinTree3.Text = "dd\r\ndd\r\ndd";
            // 
            // ipbinTree2
            // 
            this.ipbinTree2.BackColor = System.Drawing.Color.Silver;
            this.ipbinTree2.ForeColor = System.Drawing.Color.Black;
            this.ipbinTree2.Location = new System.Drawing.Point(24, 104);
            this.ipbinTree2.Name = "ipbinTree2";
            this.ipbinTree2.Size = new System.Drawing.Size(16, 1);
            this.ipbinTree2.TabIndex = 33;
            this.ipbinTree2.Text = "dd\r\ndd\r\ndd";
            // 
            // ipbinTree1
            // 
            this.ipbinTree1.BackColor = System.Drawing.Color.Silver;
            this.ipbinTree1.ForeColor = System.Drawing.Color.Black;
            this.ipbinTree1.Location = new System.Drawing.Point(24, 88);
            this.ipbinTree1.Name = "ipbinTree1";
            this.ipbinTree1.Size = new System.Drawing.Size(1, 49);
            this.ipbinTree1.TabIndex = 29;
            this.ipbinTree1.Text = "dd\r\ndd\r\ndd";
            // 
            // checkboxVGAIPBIN
            // 
            this.checkboxVGAIPBIN.AutoSize = true;
            this.checkboxVGAIPBIN.Enabled = false;
            this.checkboxVGAIPBIN.Location = new System.Drawing.Point(48, 128);
            this.checkboxVGAIPBIN.Name = "checkboxVGAIPBIN";
            this.checkboxVGAIPBIN.Size = new System.Drawing.Size(117, 24);
            this.checkboxVGAIPBIN.TabIndex = 32;
            this.checkboxVGAIPBIN.Text = "Enable VGA";
            this.checkboxVGAIPBIN.UseVisualStyleBackColor = true;
            // 
            // checkboxRegionFreeIPBIN
            // 
            this.checkboxRegionFreeIPBIN.AutoSize = true;
            this.checkboxRegionFreeIPBIN.Enabled = false;
            this.checkboxRegionFreeIPBIN.Location = new System.Drawing.Point(48, 96);
            this.checkboxRegionFreeIPBIN.Name = "checkboxRegionFreeIPBIN";
            this.checkboxRegionFreeIPBIN.Size = new System.Drawing.Size(112, 24);
            this.checkboxRegionFreeIPBIN.TabIndex = 31;
            this.checkboxRegionFreeIPBIN.Text = "Region-free";
            this.checkboxRegionFreeIPBIN.UseVisualStyleBackColor = true;
            // 
            // dropdownPatchedIPBINSource
            // 
            this.dropdownPatchedIPBINSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dropdownPatchedIPBINSource.DropDownWidth = 200;
            this.dropdownPatchedIPBINSource.Enabled = false;
            this.dropdownPatchedIPBINSource.FormattingEnabled = true;
            this.dropdownPatchedIPBINSource.ItemHeight = 20;
            this.dropdownPatchedIPBINSource.Items.AddRange(new object[] {
            "patched GDI as source",
            "unpatched GDI as source"});
            this.dropdownPatchedIPBINSource.Location = new System.Drawing.Point(208, 32);
            this.dropdownPatchedIPBINSource.MaxDropDownItems = 2;
            this.dropdownPatchedIPBINSource.Name = "dropdownPatchedIPBINSource";
            this.dropdownPatchedIPBINSource.Size = new System.Drawing.Size(200, 28);
            this.dropdownPatchedIPBINSource.TabIndex = 29;
            // 
            // checkboxAdditionalPatchingIPBIN
            // 
            this.checkboxAdditionalPatchingIPBIN.AutoSize = true;
            this.checkboxAdditionalPatchingIPBIN.Enabled = false;
            this.checkboxAdditionalPatchingIPBIN.Location = new System.Drawing.Point(16, 64);
            this.checkboxAdditionalPatchingIPBIN.Name = "checkboxAdditionalPatchingIPBIN";
            this.checkboxAdditionalPatchingIPBIN.Size = new System.Drawing.Size(276, 24);
            this.checkboxAdditionalPatchingIPBIN.TabIndex = 30;
            this.checkboxAdditionalPatchingIPBIN.Text = "Apply additional patching to IP.BIN.";
            this.checkboxAdditionalPatchingIPBIN.UseVisualStyleBackColor = true;
            this.checkboxAdditionalPatchingIPBIN.CheckedChanged += new System.EventHandler(this.CheckboxAdditionalPatchingIPBIN_CheckedChanged);
            // 
            // checkboxUsePatchedIPBIN
            // 
            this.checkboxUsePatchedIPBIN.AutoSize = true;
            this.checkboxUsePatchedIPBIN.Location = new System.Drawing.Point(16, 32);
            this.checkboxUsePatchedIPBIN.Name = "checkboxUsePatchedIPBIN";
            this.checkboxUsePatchedIPBIN.Size = new System.Drawing.Size(195, 24);
            this.checkboxUsePatchedIPBIN.TabIndex = 29;
            this.checkboxUsePatchedIPBIN.Text = "Customize IP.BIN using";
            this.checkboxUsePatchedIPBIN.UseVisualStyleBackColor = true;
            this.checkboxUsePatchedIPBIN.CheckedChanged += new System.EventHandler(this.CheckboxUsePatchedIPBIN_CheckedChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(442, 690);
            this.Controls.Add(this.groupboxStep3);
            this.Controls.Add(this.groupboxStep2);
            this.Controls.Add(this.groupboxStep1);
            this.Controls.Add(this.buttonQuit);
            this.Controls.Add(this.buttonBuildPatch);
            this.Controls.Add(this.patchBuildProgressPercentage);
            this.Controls.Add(this.patchBuildProgressBar);
            this.Controls.Add(this.pictureBox3);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.patchBuildProgressDetails);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "Universal Dreamcast Patch Builder (v1.5)";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.groupboxStep1.ResumeLayout(false);
            this.groupboxStep2.ResumeLayout(false);
            this.groupboxStep2.PerformLayout();
            this.groupboxStep3.ResumeLayout(false);
            this.groupboxStep3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.PictureBox pictureBox3;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonSelectUnpatchedGDI;
        private System.Windows.Forms.Label patchBuildProgressDetails;
        private System.Windows.Forms.Label patchBuildProgressPercentage;
        private System.Windows.Forms.ProgressBar patchBuildProgressBar;
        private System.Windows.Forms.Button buttonQuit;
        private System.Windows.Forms.Button buttonSelectPatchedGDI;
        private System.Windows.Forms.Button buttonBuildPatch;
        private System.Windows.Forms.TextBox textboxPatchFilename;
        private System.Windows.Forms.Label patchFileExtensionLabel;
        private System.Windows.Forms.Label patchFilenameLabel;
        private System.Windows.Forms.GroupBox groupboxStep1;
        private System.Windows.Forms.GroupBox groupboxStep2;
        private System.Windows.Forms.GroupBox groupboxStep3;
        private System.Windows.Forms.CheckBox checkboxAdditionalPatchingIPBIN;
        private System.Windows.Forms.CheckBox checkboxUsePatchedIPBIN;
        private System.Windows.Forms.ComboBox dropdownPatchedIPBINSource;
        private System.Windows.Forms.CheckBox checkboxVGAIPBIN;
        private System.Windows.Forms.CheckBox checkboxRegionFreeIPBIN;
        private System.Windows.Forms.Label ipbinTree1;
        private System.Windows.Forms.Label ipbinTree3;
        private System.Windows.Forms.Label ipbinTree2;
        private System.Windows.Forms.TextBox textboxGameNameIPBIN;
        private System.Windows.Forms.CheckBox checkboxCustomNameIPBIN;
        private System.Windows.Forms.Label ipbinTree5;
        private System.Windows.Forms.Label ipbinTree4;
    }
}

