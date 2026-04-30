using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Decay
{
    public partial class Decay : Form
    {
        //public static Regex ValidString = new Regex("^[a-z ]+$");
        public static bool IsPublicKeyValid = false;
        public static bool IsPlainTextValid = false;

        public Decay()
        {
            InitializeComponent();
        }

        private void btnClearPublicKey_Click(object sender, EventArgs e)
        {
            txtPublicKey.Text = string.Empty;
        }

        private void txtPublicKey_TextChanged(object sender, EventArgs e)
        {
            error("");

            if (txtPublicKey.Text == string.Empty || txtPublicKey.Text == null)
            {
                btnClearPublicKey.Enabled = false;
                IsPublicKeyValid = false;
            }
            else
            {
                btnClearPublicKey.Enabled = true;
                IsPublicKeyValid = true;

                //if (ValidString.IsMatch(txtPublicKey.Text))
                //{
                //    IsPublicKeyValid = true;
                //}
                //else
                //{
                //    error("ERROR: Public Key must contain only lower case letters and spaces");

                //    IsPublicKeyValid = false;
                //}
            }

            toggleGlobalButtons();
        }

        private void btnClearPlainText_Click(object sender, EventArgs e)
        {
            txtPlainText.Text = string.Empty;
        }

        private void txtPlainText_TextChanged(object sender, EventArgs e)
        {
            error("");

            if (txtPlainText.Text == string.Empty || txtPlainText.Text == null)
            {
                btnClearPlainText.Enabled = false;
                IsPlainTextValid = false;
            }
            else
            {
                btnClearPlainText.Enabled = true;
                IsPlainTextValid = true;

                //if (ValidString.IsMatch(txtPlainText.Text))
                //{
                //    IsPlainTextValid = true;
                //}
                //else
                //{
                //    error("ERROR: Plain Text must contain only lower case letters and spaces");

                //    IsPlainTextValid = false;
                //}
            }

            toggleGlobalButtons();
        }

        private void btnClearImage_Click(object sender, EventArgs e)
        {
            picImage.Image = null;
            btnClearImage.Enabled = false;

            toggleGlobalButtons();
        }

        private void btnChooseImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            Bitmap bitImage;
            string fileExtension = "";
            string[] acceptedExtensions = [".png"];

            dialog.Filter = "png|*.png;";

            error("");

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                fileExtension = Path.GetExtension(dialog.FileName);

                if (!acceptedExtensions.Contains(fileExtension.ToLower()))
                {
                    error("ERROR: Image must be .png");

                    picImage.Image = null;
                    btnClearImage.Enabled = false;
                }
                else
                {
                    bitImage = new Bitmap(dialog.FileName);

                    if (bitImage.Height != 512 || bitImage.Width != 512)
                    {
                        error("ERROR: Image must be 512px by 512px");

                        picImage.Image = null;
                        btnClearImage.Enabled = false;
                    }
                    else
                    {
                        picImage.Image = bitImage;
                        btnClearImage.Enabled = true;
                    }
                }
            }

            toggleGlobalButtons();
        }

        private void btnClearAll_Click(object sender, EventArgs e)
        {
            txtPublicKey.Text = string.Empty;
            txtPlainText.Text = string.Empty;
            picImage.Image = null;

            error("");

            toggleGlobalButtons();
        }

        private void btnEncrypt_Click(object sender, EventArgs e)
        {
            Image encryptedImage;
            SaveFileDialog dialog = new SaveFileDialog();
            ImageFormat fileFormat = ImageFormat.Bmp;
            string fileExtension = "";

            encryptedImage = Program.Encrypt(
                txtPublicKey.Text,
                txtPlainText.Text
            );

            dialog.Filter = "png|*.png;";
            dialog.Title = "Save Encrypted Image File";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                fileExtension = Path.GetExtension(dialog.FileName);

                switch (fileExtension.ToLower())
                {
                    case ".png":
                        fileFormat = ImageFormat.Png;
                        break;
                }

                encryptedImage.Save(dialog.FileName, fileFormat);

                picImage.Image = encryptedImage;
                btnClearImage.Enabled = true;
            }
        }

        private void btnDecrypt_Click(object sender, EventArgs e)
        {
            Bitmap image = new Bitmap(picImage.Image);

            string plainText = Program.Decrypt(txtPublicKey.Text, image);
            Console.WriteLine(plainText);

            txtPlainText.Text = plainText;
        }

        private void error(string errorText)
        {
            lblError.Text = errorText;
        }

        private void toggleClearAll()
        {
            if
            (
                txtPublicKey.Text == string.Empty &&
                txtPlainText.Text == string.Empty &&
                picImage.Image == null
            )
            {
                btnClearAll.Enabled = false;
            }
            else
            {
                btnClearAll.Enabled = true;
            }
        }

        private void toggleEncryptDecrypt()
        {
            if
            (
                IsPublicKeyValid &&
                IsPlainTextValid &&
                picImage.Image == null
            )
            {
                btnEncrypt.Visible = true;
                btnDecrypt.Visible = false;
            }
            else if
            (
                IsPublicKeyValid &&
                txtPlainText.Text == string.Empty &&
                picImage.Image != null
            )
            {
                btnEncrypt.Visible = false;
                btnDecrypt.Visible = true;
            }
            else
            {
                btnEncrypt.Visible = false;
                btnDecrypt.Visible = false;
            }
        }

        private void toggleGlobalButtons()
        {
            toggleClearAll();
            toggleEncryptDecrypt();
        }
    }
}
