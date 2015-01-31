using System;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Security.Cryptography;
using musConvert;

namespace WADex
{
    /// <summary>
    /// Possible file types to detect
    /// </summary>
    public enum FType
    {
        /// <summary>
        /// Midi file
        /// </summary>
        MID,
        /// <summary>
        /// DOOM Mus file
        /// </summary>
        MUS,
        /// <summary>
        /// RIFF WAV file
        /// </summary>
        WAV,
        /// <summary>
        /// Impulse tracker file
        /// </summary>
        IT,
        /// <summary>
        /// Extended module file
        /// </summary>
        XM,
        /// <summary>
        /// MP3 file
        /// </summary>
        MP3,
        /// <summary>
        /// RAW audio format
        /// </summary>
        RAWAUDIO,
        /// <summary>
        /// unknown file (probably doom image)
        /// </summary>
        UNKNOWN,
        /// <summary>
        /// Virtual entry
        /// </summary>
        VIRTUAL
    }

    /// <summary>
    /// Possible WAD types
    /// </summary>
    public enum WADtype
    {
        /// <summary>
        /// Internal (master) WAD
        /// </summary>
        IWAD,
        /// <summary>
        /// Patch (slave) WAD
        /// </summary>
        PWAD
    }

    /// <summary>
    /// Represents a .WAD file
    /// </summary>
	public class WADfile
	{
        /// <summary>
        /// represents a line in !INDEX.TXT
        /// </summary>
        private struct Line
        {
            /// <summary>
            /// Name of the entry in the WAD
            /// </summary>
            public string Name;
            /// <summary>
            /// File Name of this entries contents (if not virtual)
            /// </summary>
            public string FileName;
            /// <summary>
            /// offset of Data in WAD
            /// </summary>
            public int Offset;
            /// <summary>
            /// True, if Virtual entry (no file exists)
            /// </summary>
            public bool Virtual;

            /// <summary>
            /// tests if this entry is valid
            /// </summary>
            /// <returns>true, if valid</returns>
            public bool IsValid()
            {
                //Entry is valid if Name is set. If it is not virtual, a file name also needs to be present
                return !string.IsNullOrEmpty(Name) && (Virtual || !string.IsNullOrEmpty(FileName));
            }
        }

        /// <summary>
        /// Gets all WAD entries present
        /// </summary>
        public WADentry[] Entries
        { get; set; }

        /// <summary>
        /// Gets the WAD type
        /// </summary>
        public WADtype Type
        { get; set; }

        /// <summary>
        /// Opens an existing WAD file for reading
        /// </summary>
        /// <param name="fName">File name</param>
        public WADfile(string fName)
        {
            using (FileStream FS = File.OpenRead(fName))
            {
                using (BinaryReader BR = new BinaryReader(FS))
                {
                    string temp = ToString(BR.ReadBytes(4));
                    if (temp == "IWAD")
                    {
                        Type = WADtype.IWAD;
                    }
                    else if (temp == "PWAD")
                    {
                        Type = WADtype.PWAD;
                    }
                    else
                    {
                        throw new Exception("Not a WAD file. Wrong Header");
                    }
                    int NumEntries = BR.ReadInt32();
                    int Directory = BR.ReadInt32();
                    FS.Seek(Directory, SeekOrigin.Begin);
                    Entries = new WADentry[NumEntries];
                    for (int i = 0; i < NumEntries; i++)
                    {
                        int Pos = BR.ReadInt32();
                        int Len = BR.ReadInt32();
                        string Name = ToString(BR.ReadBytes(8));
                        if (Len > 0 && Pos > 0)
                        {
                            byte[] Data = new byte[Len];
                            int currentOffset = (int)FS.Position;
                            FS.Seek(Pos, SeekOrigin.Begin);
                            FS.Read(Data, 0, Len);
                            FS.Seek(currentOffset, SeekOrigin.Begin);
                            Entries[i] = new WADentry(Name, Pos, (byte[])Data.Clone());
                        }
                        else
                        {
                            Entries[i] = new WADentry(Name, 0, null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Exports all WAD file contents to the specified directory
        /// </summary>
        /// <param name="Directory">(preferable empty) Directory</param>
        public void Export(string Folder)
        {
            string MediaDir = Path.Combine(Folder, "MEDIA");
            if (!Directory.Exists(MediaDir))
            {
                Directory.CreateDirectory(MediaDir);
            }
            Dictionary<string, string> Hashes = new Dictionary<string, string>();
            using (StreamWriter SW = File.CreateText(Path.Combine(Folder,"!INDEX.TXT")))
            {
                SW.WriteLine("{0}", Type);
                foreach (WADentry e in Entries)
                {
                    if (!e.Virtual)
                    {
                        string fName = e.SafeName;
                        if (!Hashes.ContainsKey(e.Hash))
                        {
                            if (File.Exists(Path.Combine(Folder, fName)))
                            {
                                Program.Log(ConsoleColor.Yellow, "finding alternate name...");
                                int index = 0;
                                while (File.Exists(Path.Combine(Folder, string.Format("{0}_{1}", fName, index))))
                                {
                                    index++;
                                }
                                fName = string.Format("{0}_{1}", fName, index);
                            }
                            Program.Log(ConsoleColor.Green, "Creating {0}... Type: {1}", fName, e.DataType);
                            File.WriteAllBytes(Path.Combine(Folder, fName), e.Data);
                            Hashes.Add(e.Hash, fName);
                            if (e.DataType != FType.UNKNOWN)
                            {
                                Convert(e.Data, Path.Combine(MediaDir, fName));
                            }
                        }
                        else
                        {
                            Program.Log(ConsoleColor.Yellow, "{0} duplicates {1}; creating reference only", e.Name, Hashes[e.Hash]);
                        }
                        SW.WriteLine("{0,8}\t{1,12}\t{2}", e.Name, Hashes[e.Hash], e.Hash);
                    }
                    else
                    {
                        Program.Log(ConsoleColor.Green, "Create virtual entry {0}...", e.Name);
                        SW.WriteLine("{0,8}", e.Name, string.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Assembles a directory into a WAD file
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="SourceDirectory">Source directory with !INDEX.TXT</param>
        public static void Assemble(string FileName, string SourceDirectory)
        {
            //to map hashes to offsets
            Dictionary<string, int> Hashes = new Dictionary<string, int>();
            string[] Settings = File.ReadAllLines(Path.Combine(SourceDirectory, "!INDEX.TXT"));
            int entries = 0;

            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }

            using (FileStream FS = File.Create(FileName))
            {
                using (BinaryWriter BW = new BinaryWriter(FS))
                {
                    switch (Settings[0].ToUpper())
                    {
                        case "IWAD":
                            FS.Write(Encoding.ASCII.GetBytes("IWAD"), 0, 4);
                            break;
                        case "PWAD":
                            FS.Write(Encoding.ASCII.GetBytes("PWAD"), 0, 4);
                            break;
                        default:
                            throw new ArgumentException("'SourceDirectory' does not contains valid !INDEX.TXT file");
                    }
                    //write two indexes, as we need them later
                    BW.Write(0);
                    BW.Write(0);
                    BW.Flush();
                    //I am too lazy to precalculate the length of all data,
                    //so I simply create a MemoryStream for the Dictionary
                    //and append it after all data has been written
                    using (MemoryStream Dict = new MemoryStream())
                    {
                        using (BinaryWriter DictW = new BinaryWriter(Dict))
                        {
                            for (int i = 1; i < Settings.Length; i++)
                            {
                                //get next config line
                                Line L = ParseLine(Settings[i]);
                                //check if valid
                                if (L.IsValid())
                                {
                                    byte[] Data = new byte[0];

                                    //only read file if entry is not virtual
                                    if (!L.Virtual)
                                    {
                                        Program.Log(ConsoleColor.Green, "Adding entry {0} to WAD...", L.Name);
                                        Data = File.ReadAllBytes(Path.Combine(SourceDirectory, L.FileName));
                                        //create hash to check if this entry is already present
                                        string Hash = getHash(Data);

                                        //check if data is already in list
                                        if (!Hashes.ContainsKey(Hash))
                                        {
                                            //not in list (new/different entry)
                                            //adds to list and sets offset
                                            Hashes.Add(Hash, L.Offset = (int)FS.Position);
                                            //write file data at current location
                                            BW.Write(Data);
                                            BW.Flush();
                                        }
                                        else
                                        {
                                            //in list, same data exists and we only reference it to save storage
                                            Program.Log(ConsoleColor.Yellow, "Duplicate {0} will only be referenced to offset {1}", L.Name, Hashes[Hash]);
                                            L.Offset = Hashes[Hash];
                                        }
                                    }
                                    else
                                    {
                                        Program.Log(ConsoleColor.Green, "Not reading entry {0} from file as it is virtual", L.Name);
                                        //offset for virtual entries is 0
                                        L.Offset = 0;
                                    }
                                    //write WAD dictionary
                                    //always done, even if virtual
                                    DictW.Write(L.Offset);
                                    DictW.Write(Data.Length);
                                    Dict.Write(ToBytes(L.Name), 0, 8);
                                    entries++;
                                }
                                else
                                {
                                    Program.Log(ConsoleColor.Red, "Invalid Line entry: {0}", Settings[i]);
                                }
                            }
                            //write WAD indexes
                            DictW.Flush();
                            BW.Flush();
                            int pos = (int)FS.Position;
                            FS.Seek(4, SeekOrigin.Begin);
                            BW.Write(entries);
                            BW.Write(pos);
                            BW.Flush();
                            FS.Seek(0, SeekOrigin.End);
                            //write Dictionary to WAD file
                            BW.Write(Dict.ToArray());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts a byte array to string using ASCII
        /// (also strips padding nullbytes at the end)
        /// </summary>
        /// <param name="b">byte array</param>
        /// <returns>string</returns>
        public static string ToString(byte[] b)
        {
            int len = b.Length;
            //filter nullbytes
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] == 0)
                {
                    len = i;
                    break;
                }
            }
            return Encoding.ASCII.GetString(b, 0, len);
        }

        /// <summary>
        /// Converts an ASCII string to a byte array
        /// and pads it with nullbytes to be 8 bytes long
        /// </summary>
        /// <param name="s">ASCII string</param>
        /// <returns>byte array</returns>
        public static byte[] ToBytes(string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            if (b.Length < 8)
            {
                Array.Resize<byte>(ref b, 8);
            }
            return b;
        }

        /// <summary>
        /// Swaps endiannes of a given number
        /// Not used on windows
        /// </summary>
        /// <param name="Number">Number</param>
        /// <returns>reversed number</returns>
        public static int SwapEndiannes(int Number)
        {
            byte[] temp = BitConverter.GetBytes(Number);
            Array.Reverse(temp);
            return BitConverter.ToInt32(temp, 0);
        }

        /// <summary>
        /// Swaps endiannes of a given number
        /// Not used on windows
        /// </summary>
        /// <param name="Number">Number</param>
        /// <returns>reversed number</returns>
        public static short SwapEndiannes(short Number)
        {
            byte[] temp = BitConverter.GetBytes(Number);
            Array.Reverse(temp);
            return BitConverter.ToInt16(temp, 0);
        }

        /// <summary>
        /// Converts doom image data to a grayscale image
        /// (currently unused as I am lazy to code the opposing function)
        /// </summary>
        /// <param name="Data">image data</param>
        /// <param name="Destination">destination image (png)</param>
        public static void ToImage(byte[] Data, string Destination)
        {
            int w, h, i, j;
            int[] pointers;
            using (MemoryStream MS = new MemoryStream(Data))
            {
                using (BinaryReader BR = new BinaryReader(MS))
                {
                    w = BR.ReadInt16();
                    h = BR.ReadInt16();

                    //discard next 2 short variables;
                    BR.ReadInt32();

                    pointers = new int[w];
                    for (i = 0; i < w; i++)
                    {
                        pointers[i] = BR.ReadInt32();
                    }

                    BR.ReadInt16();

                    Bitmap B = new Bitmap(w,h);

                    Graphics G = Graphics.FromImage(B);
                    G.FillRectangle(Brushes.Transparent, new Rectangle(new Point(0, 0), B.Size));

                    for (j = 0; j < pointers.Length;j++ )
                    {
                        MS.Seek(pointers[j], SeekOrigin.Begin);
                        byte row = BR.ReadByte();
                        byte numPixels = BR.ReadByte();
                        if (numPixels == 0xFF && row == 0xFF)
                        {
                            //fully transparent
                            continue;
                        }
                        else
                        {
                            if (row == 0xFF)
                            {
                                row = 0x00;
                            }
                            byte[] Pixels = BR.ReadBytes(numPixels + 2);
                            for (i = 1; i < Pixels.Length - 1; i++)
                            {
                                B.SetPixel(j, row + i - 1, Color.FromArgb(Pixels[i],Pixels[i],Pixels[i]));
                            }
                        }
                    }
                    B.Save(Destination);
                    G.Dispose();
                    B.Dispose();
                }
            }
        }

        /// <summary>
        /// Converts a line to its struct representation
        /// </summary>
        /// <param name="Line">Line data</param>
        /// <returns>struct</returns>
        private static Line ParseLine(string Line)
        {
            Line L = new Line() { FileName = null, Name = null, Offset = -1 };
            if (!string.IsNullOrEmpty(Line))
            {
                string[] Parts = Line.Split('\t');
                if (Parts.Length > 1)
                {
                    L.Name = Parts[0].Trim();
                    L.FileName = Parts[1].Trim();
                    L.Virtual = false;
                }
                else
                {
                    L.Name = Line.Trim();
                    L.Virtual = true;
                }
            }
            return L;
        }

        /// <summary>
        /// Gets a hash from data
        /// </summary>
        /// <param name="Data">binary data</param>
        /// <returns>SHA1 hash</returns>
        public static string getHash(byte[] Data)
        {
            SHA1 HA = SHA1.Create();
            byte[] Hash = HA.ComputeHash(Data);
            HA.Clear();
            HA = null;
            StringBuilder retValue = new StringBuilder(Hash.Length * 2);
            foreach (byte b in Hash)
            {
                retValue.Append(b.ToString("X2"));
            }
            return retValue.ToString();
        }

        /// <summary>
        /// Converts data to its unencoded format
        /// </summary>
        /// <param name="From">Data</param>
        /// <param name="To">destination file (extension added automatically)</param>
        /// <returns>true, if successful</returns>
        public static bool Convert(byte[] From, string To)
        {
            FType FileType = WADfile.GetType(From);
            if (File.Exists(To))
            {
                File.Delete(To);
            }
            switch (FileType)
            {
                //just copy the file for these
                case FType.XM:
                case FType.IT:
                case FType.MID:
                case FType.WAV:
                case FType.MP3:
                    Program.Log(ConsoleColor.Green, "Copying DATA -> {0}", FileType);
                    File.WriteAllBytes(To + "." + FileType.ToString(), From);
                    break;
                case FType.MUS:
                    Program.Log(ConsoleColor.Yellow, "Converting MUS -> MID");
                    using (MemoryStream IN = new MemoryStream(From))
                    {
                        using (FileStream OUT = File.Create(To+".MID"))
                        {
                            MUS2MID.Convert(IN, OUT);
                        }
                    }
                    break;
                case FType.RAWAUDIO:
                    SaveAudio(From, To + ".WAV");
                    break;
                case FType.VIRTUAL:
                    Program.Log(ConsoleColor.Yellow, "Not converting virtual entry");
                    return false;
                default:
                    try
                    {
                        ToImage(From, To + ".PNG");
                    }
                    catch
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// Saves an Audio stream into a wav file
        /// </summary>
        /// <param name="From">Data</param>
        /// <param name="To">destination file</param>
        /// <returns>true, if successful</returns>
        private static bool SaveAudio(byte[] From, string To)
        {
            ushort Header, Samplerate, NumSamples, Zero;
            if (From.Length > 8)
            {
                Header = BitConverter.ToUInt16(From, 0);
                Samplerate = BitConverter.ToUInt16(From, 2);
                NumSamples = BitConverter.ToUInt16(From, 4);
                Zero = BitConverter.ToUInt16(From, 6);

                if (Zero == 0 && Header == 3 && NumSamples == From.Length - 8)
                {
                    using (FileStream FS = File.OpenWrite(To))
                    {
                        FS.Write(wavCap.Header.WaveHeader(NumSamples), 0, 44);
                        FS.Write(From, 0, From.Length);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the suggested data type
        /// </summary>
        /// <param name="Data">Data to check</param>
        /// <returns>data type</returns>
        public static FType GetType(byte[] Data)
        {
            if (Data == null || Data.Length == 0)
            {
                return FType.VIRTUAL;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("Extended Module")))
            {
                return FType.XM;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("IMPM")))
            {
                return FType.IT;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("RIFF")))
            {
                return FType.WAV;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("ID3")))
            {
                return FType.MP3;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("MThd")))
            {
                return FType.MID;
            }
            if (Cmp(Data, Encoding.UTF8.GetBytes("MUS")))
            {
                return FType.MUS;
            }
            if (Cmp(Data, new byte[] { 0x03, 0x00 }))
            {
                return FType.RAWAUDIO;
            }
            return FType.UNKNOWN;
        }

        /// <summary>
        /// compares two byte arrays, B must be in A
        /// </summary>
        /// <param name="A">Main Array</param>
        /// <param name="B">Array to search for</param>
        /// <returns>true, if A begins with B</returns>
        private static bool Cmp(byte[] A, byte[] B)
        {
            for (int i = 0; i < A.Length && i < B.Length; i++)
            {
                if (A[i] != B[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
