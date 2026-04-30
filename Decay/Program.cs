using System.Reflection;
using System.Text.RegularExpressions;

namespace Decay
{
    internal static class Program
    {
        static readonly string PrivateKey = "Sphinx of black quartz, judge my vow.";
        static readonly string Alphabet = "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz 0123456789`~!@#$%^&*-_=+()[]{}<>\\|;:'\",./?";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Decay());
        }

        /// <summary>
        /// Encrypt a plaintext message into the provided image
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="plainText"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Bitmap Encrypt(string publicKey, string plainText)
        {
            string publicShiftedAlphabet = "";
            string privateShiftedAlphabet = "";
            byte[] originalImageByte = Properties.Resources.Steganography;
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            Bitmap originalImage;
            Bitmap encryptedImage;
            Color currentPixelColor;
            Color encryptedPixelColor;
            Random random = new Random();
            int publicKeyIndex = 0;
            int privateKeyIndex = 0;
            int plainTextIndex = 0;
            int encryptedAlphaIndex = 0;
            double pixelTotal = 0.0;
            double noiseEndY = 0.0;
            double noiseEndX = 0.0;
            double messageEndY = 0.0;
            double messageEndX = 0.0;
            bool noise = true;
            char currentChar;

            using (MemoryStream stream = new MemoryStream(originalImageByte))
            {
                originalImage = new Bitmap(stream);
            }

            encryptedImage = new Bitmap(originalImage);

            pixelTotal = originalImage.Height * originalImage.Width;
            noiseEndY = ((pixelTotal - (plainText.Length + 2)) / 2) / originalImage.Height; // plainText.Length + 2 here for message begining '[' and end ']' chars
            noiseEndX = ((pixelTotal - (plainText.Length + 2)) / 2) % originalImage.Width; // plainText.Length + 2 here for message begining '[' and end ']' chars

            // if each pixel has 3 colors R0, G1, and B2, then each row has (originalImage.Width * 3) - 1 colors indexes and the colorTotal is originalImage.Height * originalImage.Width * 3
            // need to find the center of the colors

            // message start pixel = random(number of pixels needed to encode start and end pixels, pixel total - message length)
            // message end pixel = message start pixel + message length
            // pixel coordinate = floor(pixel total / pixel number), floor(pixel total % pixel number)

            // encoded pixel coordinate = (random(0, 81) * 1000000) + (pixelX * 1000) + pixelY
            // decoded pixelX = floor((encoded % 1000000) / 1000)
            // decoded pixelY = (encoded % 1000000) % 1000

            // 4 pixels per encoded pixel coordinate, 8 needed to encode start and end

            for (int row = 0; row < originalImage.Height; row++)
            {
                for (int column = 0; column < originalImage.Width; column++)
                {
                    currentPixelColor = originalImage.GetPixel(row, column);

                    for (int rgb = 0; rgb < 3; rgb++)
                    {
                        if (noise)
                        {
                            if
                            (
                                row == (int)Math.Floor(noiseEndY) &&
                                column == (int)Math.Floor(noiseEndX)
                            )
                            {
                                currentChar = '[';

                                noise = false;
                            }
                            else
                            {
                                currentChar = Alphabet[random.Next(Alphabet.Length)];
                            }
                        }
                        else
                        {
                            if (plainTextIndex == plainText.Length)
                            {
                                currentChar = ']';

                                noise = true;
                            }
                            else
                            {
                                currentChar = plainText[plainTextIndex];
                                publicShiftedAlphabet =
                                    ShiftAlphabet(Alphabet.IndexOf(publicKey[publicKeyIndex]));
                                privateShiftedAlphabet =
                                    ShiftAlphabet(Alphabet.IndexOf(PrivateKey[privateKeyIndex]));

                                currentChar = publicShiftedAlphabet
                                    [Alphabet.IndexOf(currentChar)];
                                currentChar = privateShiftedAlphabet
                                    [Alphabet.IndexOf(currentChar)];

                                publicKeyIndex++;
                                if (publicKeyIndex == publicKey.Length)
                                {
                                    publicKeyIndex = 0;
                                }

                                privateKeyIndex++;
                                if (privateKeyIndex == PrivateKey.Length)
                                {
                                    privateKeyIndex = 0;
                                }

                                plainTextIndex++;
                            }
                        }

                        encryptedAlphaIndex = Alphabet.IndexOf(currentChar);

                        if (currentPixelColor.R + Alphabet.Length - 1 <= 255)
                        {
                            encryptedAlphaIndex *= -1;
                        }

                        switch (rgb)
                        {
                            case 0:
                                red = (byte)(currentPixelColor.R + encryptedAlphaIndex);
                                break;

                            case 1:
                                green = (byte)(currentPixelColor.G + encryptedAlphaIndex);
                                break;

                            case 2:
                                blue = (byte)(currentPixelColor.B + encryptedAlphaIndex);
                                break;

                            default:
                                red = currentPixelColor.R;
                                green = currentPixelColor.G;
                                blue = currentPixelColor.B;
                                break;
                        }
                    }

                    encryptedPixelColor = Color.FromArgb(red, green, blue);
                    encryptedImage.SetPixel(row, column, encryptedPixelColor);
                }
            }

            return encryptedImage;
        }

        /// <summary>
        /// Decrypt message hidden in the provided image
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="encryptedImage"></param>
        /// <returns></returns>
        public static string Decrypt(string publicKey, Bitmap encryptedImage)
        {
            string plainText = "";
            string privateShiftedAlphabet = "";
            string publicShiftedAlphabet = "";
            byte[] originalImageByte = Properties.Resources.Steganography;
            Bitmap originalImage;
            Bitmap decryptedImage = new Bitmap(encryptedImage);
            Color encryptedCurrentPixelColor;
            Color originalCurrentPixelColor;
            int pixelDifference = 0;
            char currentChar;
            int publicKeyIndex = 0;
            int privateKeyIndex = 0;
            bool noise = true;

            using (MemoryStream stream = new MemoryStream(originalImageByte))
            {
                originalImage = new Bitmap(stream);
            }

            for (int row = 0; row < originalImage.Height; row++)
            {
                for (int column = 0; column < originalImage.Width; column++)
                {
                    if (noise)
                    {
                        if (Alphabet[pixelDifference] == '[')
                        {
                            noise = false;
                        }
                    }
                    else
                    {
                        encryptedCurrentPixelColor = encryptedImage.GetPixel(row, column);
                        originalCurrentPixelColor = originalImage.GetPixel(row, column);
                        pixelDifference = Math.Abs(
                            encryptedCurrentPixelColor.R -
                            originalCurrentPixelColor.R
                        );
                        if (Alphabet[pixelDifference] == ']')
                        {
                            noise = true;
                        }
                        else
                        {
                            currentChar = Alphabet[pixelDifference];

                            privateShiftedAlphabet = ShiftAlphabet(Alphabet.IndexOf(PrivateKey[privateKeyIndex]));
                            publicShiftedAlphabet = ShiftAlphabet(Alphabet.IndexOf(publicKey[publicKeyIndex]));

                            currentChar = Alphabet[
                                privateShiftedAlphabet
                                .IndexOf(currentChar)
                            ];
                            currentChar = Alphabet[
                                publicShiftedAlphabet
                                .IndexOf(currentChar)
                            ];

                            publicKeyIndex++;
                            if (publicKeyIndex == publicKey.Length)
                            {
                                publicKeyIndex = 0;
                            }

                            privateKeyIndex++;
                            if (privateKeyIndex == PrivateKey.Length)
                            {
                                privateKeyIndex = 0;
                            }

                            plainText += currentChar;
                        }
                    }
                }
            }

            //foreach (char currentChar in encryptedText)
            //{

            //}

            return plainText;
        }

        /// <summary>
        /// Shift Alphabet to difference index
        /// </summary>
        /// <param name="shiftIndex"></param>
        /// <returns></returns>
        private static string ShiftAlphabet(int shiftIndex)
        {
            return Alphabet.Substring(shiftIndex, Alphabet.Length - shiftIndex) +
                Alphabet.Substring(0, shiftIndex);
        }
    }
}