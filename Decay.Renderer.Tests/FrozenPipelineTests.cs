using System.Security.Cryptography;
using System.Text;
using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    /// <summary>
    /// Golden vectors for the FROZEN render pipelines: Glitch (pipeline 1,
    /// docs/render-pipeline-v1.md) and Rot (pipeline 2,
    /// docs/render-pipeline-v2-rot.md). Each vector is an image exactly as
    /// v1.0 makes it: fixed container bytes (sealed with 1,000 iterations, the
    /// salt 01..10 and the nonce 65..70), a carrier, the art the pipeline
    /// renders, the exported image and the reveal the receiver gets.
    ///
    /// Round-trip tests can't catch a change to the pipeline math, because the
    /// encoder and decoder change together. These can. If one fails, the change
    /// alters how images already sent un-rot: revert it. Never update the
    /// expected hashes; new behavior belongs in a new pipeline id.
    /// </summary>
    public class FrozenPipelineTests
    {
        public sealed record Vector(
            string Message,
            string Passphrase,
            int Width,
            int Height,
            int Intensity,
            string ContainerHex,
            string CarrierSha256,
            string ArtSha256,
            string ExportedSha256,
            string RevealSha256
        );

        private static readonly Dictionary<string, Vector> Glitch = new Dictionary<string, Vector>
        {
            ["GlitchFree"] = new Vector(
                "The glitch is the letter.",
                "frozen glitch",
                72,
                48,
                20,
                "444341590101140102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F7000000019081734C156"
                    + "B8E22EE8E2540A0DDB0D302F922F0D7929AAD2BF468A457F6EEADA55D8DEC28410C916BF",
                "BD3022258417298698BFDE532086607A426BEA6BA050F6144E63A0B982F23719",
                "9048309F829DC6CEB1A7E7725280ACE1BDCEBBDF4BDA30F65188B07AFDC15B10",
                "863FC394267469A69B9A8A707DD6DB0B87C5972102E5CE16176867F4DD5150F1",
                "9B7476530C34EFA63B4E76E76AFE4C2A58742F73E6D88BA608D9F10E95D42D54"
            ),
            ["GlitchDefault"] = new Vector(
                "x",
                "frozen glitch",
                37,
                61,
                100,
                "444341590101000102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F7000000001240D85B81D"
                    + "508A50BD31DAA00FD4950083",
                "056D743C32BD95D0D8C1C8C6025938CBBBB254DECA1CE07B8008B68CED832023",
                "3F9D4E109BD2B0B8B761CB954494777271AC3CF5F4DC37AD770254B0AA6E3F19",
                "818D043ED9F211D4E2D74092599810F321E6A70564E6633244686DE36E2FAF0C",
                "A630A2C179CD2FB3F15F3F3252FFA9944160D099614705553A232D88CBE607DB"
            ),
            ["GlitchMax"] = new Vector(
                "Every tear is ciphertext.",
                "frozen glitch",
                64,
                64,
                255,
                "444341590101FF0102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F70000000191909349348"
                    + "F4FF3FEAF8540A0DDB1A313ADA261A7938B7D4BFB8A03541D23E30EF3130D055F66E91F7",
                "AFF79A84141428A4CB73C97CDCDD430A495D22D7EAB0D189CBA8CDAAAC90190A",
                "0F072C79C63CCC7EF0E61D25F49AFDD59E79B2CCE0BD5E68C35A6C5C15E8BD9B",
                "5C8626B85F9912F361A9349321B9B4EF2E7D608F131775E8D7C8090054A74BA2",
                "18EF9250151270100A0FDAD2827F6E7777056DD85CB69A2732E3ABFFF2F6546E"
            ),
        };

        private static readonly Dictionary<string, Vector> Rot = new Dictionary<string, Vector>
        {
            ["RotDefault"] = new Vector(
                "Meet me at 9pm!\n\t\u00C9tude \U0001F9A0 ok\r~",
                "frozen rot",
                96,
                64,
                100,
                "444341590102640102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F700000002100742D4FC2"
                    + "C9BDAFA5C6E4E08F63EDC3C7FB05AFC56F79FB7A3C6EA154F3E7132EF872367B67C652511995E2EED714DC4B",
                "C2ED8AC1B4188DEF7857D21FF05FD779ECD9B0B4C49B80C0BA4F4D6F1FFD1251",
                "91D0A992AAA35F9064885FD17E8ABE8386BE88DC3EA3A3C67984761D274E6F3F",
                "6FD317AA86242592C841C2FA70CBDEDEF18628F8ECF5E8D89EE61B4BEA591616",
                "08EF39DDD50022024814E38770683E06931BCC1A23CB77395D53AE1B19EAC2AD"
            ),
            ["RotParamZero"] = new Vector(
                "A",
                "frozen rot",
                53,
                81,
                100,
                "444341590102000102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F70000000010C13AEF989"
                    + "C9FE094BE67C91FF1B872BAB",
                "02D920CBFEC68C2C2D41AD14C6ECC760EBE9039A6BAACC101845A9ABA34974C1",
                "67879A2E08D5A22FA42D36521EDBF4AD5EC8BECB4274D7EACA44DCEB581E5B76",
                "EB21885C559759C6D2E2FD841FB85509E0EB15591A3A61E82C0B5E631D44639A",
                "8335E066C615E9EBE64304F5DE2BA737E100CB3AF752A0DB32DB73B8AC64096A"
            ),
            ["RotMaxLong"] = new Vector(
                string.Concat(Enumerable.Repeat("rot spreads from the inside out. ", 6)),
                "frozen rot",
                80,
                80,
                255,
                "444341590102FF0102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F70000000C63F7E3C1B91"
                    + "D4AAEAA5D6B7F9997CA3A4EE4CE4BE906272A8E3C7AD211BE9F83070273DF17B14E2E54BCDFDAEE6406A529156E3B5CD"
                    + "00A48F8C74E0A2D8E37675F8D523A3527A93B13EE4D75084682194723EC49B292E7BDD1F3C119CD6C480FB35032DB770"
                    + "56530DC665CC3209D88AD2EF5786FE646D07A8DD0FE587E73F5E5C54FC1F8F812C6573F17C8E9E01FB8BD667935E2451"
                    + "A856CC860E17BE7DFE46468DC0486E6E06E00E989DF4984BD0B4B69F7E3BF669F08472FC6781A71C059664B428000441"
                    + "38224A9C74E2D6DE3FA49B4531A6577237",
                "ED266F90AEE0673CD83EAA053EC2DE7A08E462088C0C1DD5EF4AE7869CD513B9",
                "883361AA9AFE6115FBD21DC9C3574607B198F2CE31C3EA23AAB01197778B6224",
                "EDD072C4CF212E6DC4CB8E1F09D224877C7C896DD70480FE6DCAD84BF3A4ACBD",
                "8ADBEC6E8D5FFCCD52DFD06D7C06782D06AE7048C83FC9ABAEB43092D3D559AF"
            ),
        };

        [Theory]
        [InlineData("GlitchFree")]
        [InlineData("GlitchDefault")]
        [InlineData("GlitchMax")]
        public void Glitch_RendersTheFrozenArt(string name)
        {
            Vector v = Glitch[name];
            PixelBuffer carrier = Carrier(v);
            DecaySequence sequence = DecaySequence.FromContainer(Convert.FromHexString(v.ContainerHex))!;

            Assert.Equal(v.Intensity, sequence.Intensity);
            Assert.Equal(v.ArtSha256, Sha256(sequence.RenderFinal(carrier)));
        }

        [Theory]
        [InlineData("GlitchFree")]
        [InlineData("GlitchDefault")]
        [InlineData("GlitchMax")]
        public void Glitch_FrozenImage_DecryptsAndUnrots(string name)
        {
            Vector v = Glitch[name];
            byte[] container = Convert.FromHexString(v.ContainerHex);
            PixelBuffer received = DecaySequence.FromContainer(container)!.RenderFinal(Carrier(v));

            DecayCipher.EmbedContainer(container, received);
            Assert.Equal(v.ExportedSha256, Sha256(received));

            DecryptedArt result = DecayArtist.Decrypt(received, v.Passphrase);

            Assert.Equal(v.Message, result.PlainText);
            Assert.Null(result.Rot);
            Assert.Equal(v.RevealSha256, Sha256(result.Sequence!.Unrot(received)));
        }

        [Theory]
        [InlineData("RotDefault")]
        [InlineData("RotParamZero")]
        [InlineData("RotMaxLong")]
        public void Rot_RendersTheFrozenArt(string name)
        {
            Vector v = Rot[name];
            PixelBuffer carrier = Carrier(v);
            RotDecay rot = RotDecay.FromContainer(
                Convert.FromHexString(v.ContainerHex),
                v.Message,
                v.Passphrase
            )!;

            Assert.Equal(v.Intensity, rot.Intensity);
            Assert.Equal(v.ArtSha256, Sha256(rot.Apply(carrier)));
        }

        [Theory]
        [InlineData("RotDefault")]
        [InlineData("RotParamZero")]
        [InlineData("RotMaxLong")]
        public void Rot_FrozenImage_DecryptsAndRestores(string name)
        {
            Vector v = Rot[name];
            byte[] container = Convert.FromHexString(v.ContainerHex);
            PixelBuffer received = RotDecay.FromContainer(container, v.Message, v.Passphrase)!
                .Apply(Carrier(v));

            DecayCipher.EmbedContainer(container, received);
            Assert.Equal(v.ExportedSha256, Sha256(received));

            DecryptedArt result = DecayArtist.Decrypt(received, v.Passphrase);

            Assert.Equal(v.Message, result.PlainText);
            Assert.Null(result.Sequence);
            Assert.Equal(v.RevealSha256, Sha256(result.Rot!.Restore(received)));
        }

        [Fact]
        public void Xoshiro256_StreamIsFrozen()
        {
            Xoshiro256 fromHash = new Xoshiro256(
                Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray()
            );
            Xoshiro256 fromUlong = new Xoshiro256(0x0123456789ABCDEFUL);
            Xoshiro256 allZero = new Xoshiro256(new byte[32]);

            Assert.Equal(
                new[] { 0xBB9D7F61432502D1UL, 0xA04F6407D9BE6E8BUL, 0x3F399295D13AC688UL, 0xAFB8958D3DC3930CUL },
                new[] { fromHash.Next(), fromHash.Next(), fromHash.Next(), fromHash.Next() }
            );
            Assert.Equal(
                new[] { 0xA2C2A42038D4EC3DUL, 0x05FC25D0738E7B0FUL, 0x625E7BFF938E701EUL, 0x1BA4DDC6FE2B5726UL },
                new[] { fromUlong.Next(), fromUlong.Next(), fromUlong.Next(), fromUlong.Next() }
            );

            // An all-zero hash seed sets state[0] to 0x9E3779B97F4A7C15 (spec).
            Assert.Equal(
                new[] { 0x0000000000000000UL, 0xE032CDB00BE7D8E3UL },
                new[] { allZero.Next(), allZero.Next() }
            );
        }

        [Fact]
        public void RotAlphabet_MappingIsFrozen()
        {
            // One byte per Unicode scalar value, U+0000..U+10FFFF without surrogates.
            byte[] indices = new byte[1112064];
            int at = 0;

            for (int value = 0; value <= 0x10FFFF; value++)
            {
                if (value >= 0xD800 && value <= 0xDFFF)
                {
                    continue;
                }

                indices[at++] = (byte)RotAlphabet.IndexOf(new Rune(value));
            }

            Assert.Equal(
                "C01D9F8ED4CE636A20F5902673E71E60429044F08D55B3582620283858E7E7A4",
                Convert.ToHexString(SHA256.HashData(indices))
            );
        }

        // Integer-only test carrier, so the vectors never depend on the
        // (unfrozen) presets. Its hash is pinned too.
        private static PixelBuffer Carrier(Vector v)
        {
            PixelBuffer image = new PixelBuffer(v.Width, v.Height);

            for (int y = 0; y < v.Height; y++)
            {
                for (int x = 0; x < v.Width; x++)
                {
                    int i = (y * v.Width + x) * PixelBuffer.BytesPerPixel;

                    image.Pixels[i] = (byte)(x * 255 / Math.Max(1, v.Width - 1));
                    image.Pixels[i + 1] = (byte)(y * 255 / Math.Max(1, v.Height - 1));
                    image.Pixels[i + 2] = (byte)((x * 7 + y * 13) ^ (x * y));
                    image.Pixels[i + 3] = 255;
                }
            }

            Assert.Equal(v.CarrierSha256, Sha256(image));

            return image;
        }

        private static string Sha256(PixelBuffer image)
        {
            return Convert.ToHexString(SHA256.HashData(image.Pixels));
        }
    }
}
