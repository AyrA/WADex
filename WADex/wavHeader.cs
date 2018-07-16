using System;
using System.IO;
using System.Text;

namespace wavCap
{
    public static class Header
    {
        private const int HEADER_LEN = 44;

        public enum CommonQuality : uint
        {
            StudioQuality = 48000,
            CDQuality = 44100,
            LowQuality = 22050,
            TelephoneQuality = 11250
        }

        /// <summary>
        /// Generates a RIFF Header for a PCM Wave File
        /// </summary>
        /// <param name="rawLength">Length of the RAW Bytes Stream</param>
        /// <param name="Freq">Frequency in Hertz: (Default: 44100)</param>
        /// <param name="Chan">Channels: (Default: 2)</param>
        /// <param name="Bits">Bits Per Sample (Default: 16)</param>
        /// <returns>RIFF Header for PCM RAW Data</returns>
        public static byte[] WaveHeader(uint rawLength, uint Freq = (uint)CommonQuality.CDQuality, uint Chan = 2, uint Bits = 16)
        {
            byte[] Header = new byte[HEADER_LEN];

            using (var MS = new MemoryStream())
            {
                using (var BW = new BinaryWriter(MS))
                {
                    BW.Write(s2b("RIFF"));
                    BW.Write((rawLength + HEADER_LEN - 8));
                    BW.Write(s2b("WAVE"));
                    BW.Write(s2b("fmt "));
                    BW.Write(BitConverter.GetBytes(16u));
                    BW.Write(BitConverter.GetBytes((ushort)1));
                    BW.Write(new byte[] { (byte)(Chan == 2 ? 2 : 1), 0 });
                    BW.Write(BitConverter.GetBytes(Freq));
                    BW.Write(BitConverter.GetBytes((uint)(Freq * (Chan == 2 ? 2 : 1) * Bits / 8)));
                    BW.Write(BitConverter.GetBytes((ushort)((Chan == 2 ? 2 : 1) * Bits / 8)));
                    BW.Write(BitConverter.GetBytes((ushort)Bits));

                    //"DATA" Chunk
                    BW.Write(s2b("data"));
                    BW.Write(BitConverter.GetBytes(rawLength));

                    BW.Flush();

                    return MS.ToArray();
                }
            }
        }

        private static byte[] s2b(string s)
        {
            return s == null ? null : Encoding.Default.GetBytes(s);
        }
    }
}