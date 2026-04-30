namespace Decay
{
    partial class Decay
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblPublicKey = new Label();
            txtPublicKey = new TextBox();
            lblPlainText = new Label();
            txtPlainText = new TextBox();
            picImage = new PictureBox();
            lblImage = new Label();
            btnEncrypt = new Button();
            btnClearAll = new Button();
            btnChooseImage = new Button();
            btnDecrypt = new Button();
            btnClearPlainText = new Button();
            btnClearPublicKey = new Button();
            btnClearImage = new Button();
            lblError = new Label();
            ((System.ComponentModel.ISupportInitialize)picImage).BeginInit();
            SuspendLayout();
            // 
            // lblPublicKey
            // 
            lblPublicKey.AutoSize = true;
            lblPublicKey.Location = new Point(12, 23);
            lblPublicKey.Name = "lblPublicKey";
            lblPublicKey.Size = new Size(62, 15);
            lblPublicKey.TabIndex = 0;
            lblPublicKey.Text = "Public Key";
            // 
            // txtPublicKey
            // 
            txtPublicKey.Location = new Point(12, 41);
            txtPublicKey.Name = "txtPublicKey";
            txtPublicKey.Size = new Size(265, 23);
            txtPublicKey.TabIndex = 0;
            txtPublicKey.TextChanged += txtPublicKey_TextChanged;
            // 
            // lblPlainText
            // 
            lblPlainText.AutoSize = true;
            lblPlainText.Location = new Point(12, 81);
            lblPlainText.Name = "lblPlainText";
            lblPlainText.Size = new Size(57, 15);
            lblPlainText.TabIndex = 2;
            lblPlainText.Text = "Plain Text";
            // 
            // txtPlainText
            // 
            txtPlainText.Location = new Point(12, 99);
            txtPlainText.MaxLength = 262142;
            txtPlainText.Multiline = true;
            txtPlainText.Name = "txtPlainText";
            txtPlainText.Size = new Size(265, 245);
            txtPlainText.TabIndex = 1;
            txtPlainText.TextChanged += txtPlainText_TextChanged;
            // 
            // picImage
            // 
            picImage.BackColor = SystemColors.Window;
            picImage.BackgroundImageLayout = ImageLayout.None;
            picImage.BorderStyle = BorderStyle.Fixed3D;
            picImage.Location = new Point(281, 35);
            picImage.Name = "picImage";
            picImage.Size = new Size(360, 309);
            picImage.TabIndex = 4;
            picImage.TabStop = false;
            // 
            // lblImage
            // 
            lblImage.AutoSize = true;
            lblImage.Location = new Point(281, 14);
            lblImage.Name = "lblImage";
            lblImage.Size = new Size(40, 15);
            lblImage.TabIndex = 5;
            lblImage.Text = "Image";
            // 
            // btnEncrypt
            // 
            btnEncrypt.Location = new Point(565, 349);
            btnEncrypt.Name = "btnEncrypt";
            btnEncrypt.Size = new Size(75, 23);
            btnEncrypt.TabIndex = 3;
            btnEncrypt.Text = "Encrypt";
            btnEncrypt.UseVisualStyleBackColor = true;
            btnEncrypt.Visible = false;
            btnEncrypt.Click += btnEncrypt_Click;
            // 
            // btnClearAll
            // 
            btnClearAll.Location = new Point(9, 349);
            btnClearAll.Name = "btnClearAll";
            btnClearAll.Size = new Size(75, 23);
            btnClearAll.TabIndex = 7;
            btnClearAll.Text = "Clear All";
            btnClearAll.UseVisualStyleBackColor = true;
            btnClearAll.Click += btnClearAll_Click;
            // 
            // btnChooseImage
            // 
            btnChooseImage.Location = new Point(281, 349);
            btnChooseImage.Name = "btnChooseImage";
            btnChooseImage.Size = new Size(92, 23);
            btnChooseImage.TabIndex = 2;
            btnChooseImage.Text = "Choose Image";
            btnChooseImage.UseVisualStyleBackColor = true;
            btnChooseImage.Click += btnChooseImage_Click;
            // 
            // btnDecrypt
            // 
            btnDecrypt.Location = new Point(565, 349);
            btnDecrypt.Name = "btnDecrypt";
            btnDecrypt.Size = new Size(75, 23);
            btnDecrypt.TabIndex = 3;
            btnDecrypt.Text = "Decrypt";
            btnDecrypt.UseVisualStyleBackColor = true;
            btnDecrypt.Visible = false;
            btnDecrypt.Click += btnDecrypt_Click;
            // 
            // btnClearPlainText
            // 
            btnClearPlainText.Enabled = false;
            btnClearPlainText.Location = new Point(233, 70);
            btnClearPlainText.Name = "btnClearPlainText";
            btnClearPlainText.Size = new Size(43, 23);
            btnClearPlainText.TabIndex = 5;
            btnClearPlainText.Text = "Clear";
            btnClearPlainText.UseVisualStyleBackColor = true;
            btnClearPlainText.Click += btnClearPlainText_Click;
            // 
            // btnClearPublicKey
            // 
            btnClearPublicKey.Location = new Point(233, 12);
            btnClearPublicKey.Name = "btnClearPublicKey";
            btnClearPublicKey.Size = new Size(43, 23);
            btnClearPublicKey.TabIndex = 4;
            btnClearPublicKey.Text = "Clear";
            btnClearPublicKey.UseVisualStyleBackColor = true;
            btnClearPublicKey.Click += btnClearPublicKey_Click;
            // 
            // btnClearImage
            // 
            btnClearImage.Enabled = false;
            btnClearImage.Location = new Point(596, 7);
            btnClearImage.Name = "btnClearImage";
            btnClearImage.Size = new Size(44, 23);
            btnClearImage.TabIndex = 6;
            btnClearImage.Text = "Clear";
            btnClearImage.UseVisualStyleBackColor = true;
            btnClearImage.Click += btnClearImage_Click;
            // 
            // lblError
            // 
            lblError.AutoSize = true;
            lblError.ForeColor = Color.Red;
            lblError.Location = new Point(374, 563);
            lblError.Name = "lblError";
            lblError.Size = new Size(0, 15);
            lblError.TabIndex = 13;
            // 
            // Decay
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(585, 379);
            Controls.Add(lblError);
            Controls.Add(btnClearImage);
            Controls.Add(btnClearPublicKey);
            Controls.Add(btnClearPlainText);
            Controls.Add(btnDecrypt);
            Controls.Add(btnChooseImage);
            Controls.Add(btnClearAll);
            Controls.Add(btnEncrypt);
            Controls.Add(lblImage);
            Controls.Add(picImage);
            Controls.Add(txtPlainText);
            Controls.Add(lblPlainText);
            Controls.Add(txtPublicKey);
            Controls.Add(lblPublicKey);
            Name = "Decay";
            Text = "Decay";
            ((System.ComponentModel.ISupportInitialize)picImage).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblPublicKey;
        private TextBox txtPublicKey;
        private Label lblPlainText;
        private TextBox txtPlainText;
        private PictureBox picImage;
        private Label lblImage;
        private Button btnEncrypt;
        private Button btnClearAll;
        private Button btnChooseImage;
        private Button btnDecrypt;
        private Button btnClearPlainText;
        private Button btnClearPublicKey;
        private Button btnClearImage;
        private Label lblError;
    }
}
