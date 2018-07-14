using System;
using System.Text;
using System.IO;

namespace WADex
{
    class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">arguments</param>
        private static void Main(string[] args)
        {
            WADfile WF;
            if (args.Length < 2)
            {
                Help(args.Length == 1 ? args[0].ToUpper()[0] : ' ');
            }
            else if (args.Length == 2)
            {
                if (args[0].ToUpper() == "I")
                {
                    if (File.Exists(args[1]))
                    {
                        try
                        {
                            WF = new WADfile(args[1]);
                        }
                        catch (Exception ex)
                        {
                            WF = null;
                            Log(ConsoleColor.Red, "Error: {0}", ex.Message);
                            return;
                        }
                        Console.WriteLine(WF.Type.ToString());
                        Console.WriteLine("NAME;FILENAME;OFFSET;LENGTH;TYPE;HASH");
                        foreach (WADentry e in WF.Entries)
                        {
                            Console.WriteLine("{0};{1};{2};{3};{4};{5}", e.Name, e.SafeName, e.Offset, e.Length, e.DataType, e.Hash);
                        }
                    }
                    else
                    {
                        Log(ConsoleColor.Red, "File not found");
                    }
                }
                else if (args[0].ToUpper() == "C")
                {
                    Convert(args[1]);
                }
                else
                {
                    Help(' ');
                }
            }
            else if (args.Length == 3)
            {
                switch (args[0].ToUpper())
                {
                    case "A":
                        if (Directory.Exists(args[2]))
                        {
                            try
                            {
                                WADfile.Assemble(args[1], args[2]);
                                Console.WriteLine("Done");
                            }
                            catch (Exception ex)
                            {
                                WF = null;
                                Log(ConsoleColor.Red, "Error: {0}", ex.Message);
                                return;
                            }

                        }
                        else
                        {
                            Log(ConsoleColor.Red, "Directory not found: {0}", args[2]);
                        }
                        break;
                    case "E":
                        try
                        {
                            WF = new WADfile(args[1]);
                        }
                        catch (Exception ex)
                        {
                            WF = null;
                            Log(ConsoleColor.Red, "Error: {0}", ex.Message);
                            return;
                        }
                        if (Directory.Exists(args[2]))
                        {
                            WF.Export(args[2]);
                            Console.WriteLine("Done");
                        }
                        else
                        {
                            Log(ConsoleColor.Red, "Directory not found: {0}", args[2]);
                        }
                        break;
                    case "C":
                        Convert(args[1], args[2]);
                        break;
                    default:
                        Log(ConsoleColor.Red, "Invalid Operation: {0}", args[0]);
                        break;
                }
            }
        }

        private static void Convert(string From, string To)
        {
            WADfile.Convert(File.ReadAllBytes(From), To);
        }

        private static void Convert(string From)
        {
            int last = From.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            int dot = From.LastIndexOf('.');
            string To = From;
            FType FileType = WADfile.GetType(File.ReadAllBytes(From));
            //check if file has extension, if so, cut off
            if (dot > last)
            {
                To = From.Substring(0, dot);
            }
            if (FileType == FType.MUS)
            {
                To += ".MID";
            }
            else if (FileType == FType.RAWAUDIO)
            {
                To += ".WAV";
            }

            else
            {
                To += "." + FileType.ToString();
            }
            //very simple check, if the destination is the same as the source, ignoring character case
            if (File.Exists(To) && WADfile.getHash(File.ReadAllBytes(From)) == WADfile.getHash(File.ReadAllBytes(To)))
            {
                Log(ConsoleColor.Yellow, "File would be identical after generating destination name. Not converting");
            }
            else
            {
                Convert(From, To);
            }
        }

        private static string ToHex(byte[] Head)
        {
            StringBuilder SB = new StringBuilder(Head.Length);
            foreach (byte b in Head)
            {
                if (b < ' ' || b > 126)
                {
                    SB.Append('.');
                }
                else
                {
                    SB.Append((char)b);
                }
            }
            return SB.ToString();
        }

        /// <summary>
        /// displays global or argument specific help
        /// </summary>
        /// <param name="Operation">operation to show the help.</param>
        private static void Help(char Operation)
        {
            switch (Operation)
            {
                case 'A':
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

A operation
-----------

The A operation assembles a WAD file from the specified directory path.
The directory needs to have a valid '!INDEX.TXT' file present.

If the WAD file is present, it is overwritten

The importer will do the folowing for you:
- recalculating SHA hashes (the index file hashes are not modified)
- importing all referenced files into the WAD in the order you have specified
  them in the index file.
- checking for identical data to save space.
- update the references in the WAD file
");
                    break;
                case 'E':
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

E operation
-----------

The E operation extracts all items into the supplied directory
A file named !INDEX.TXT is created, that is required for the
'A' operation. The file can be edited if you make changes to the
extracted resources.

if the directory already contains files, they are overwritten
additionally present files are left as is.

File format:
The first line is either IWAD or PWAD.
IWAD is a 'main' WAD file, a PWAD is a 'patch' file,
that alters the game at runtime. You must have 1 IWAD per game
and can have many PWAD files.
All folowing lines are in this format:
<NAME>[<TAB><FILENAME><TAB><HASH>]

NAME      Name of the entry (padded to 8 chars with spaces on the left)
FILENAME  Filename for the entry (padding to 12 chars with spaces)
          if this is missing the entry is of virtual nature and serves
          to group entries together.
HASH      SHA1 hash of the entry. Not present for virtual entries
TAB       Tab char

You can edit resources and the file freely.
- You do not need to keep the padding
- You do not need to update the HASH value
- You can change between IWAD and PWAD

If you add resources, add them in the correct position in the index file.
Example: If you add a 'Sprite'-type resource, then add it between the
matching virtual entries in the index file.

See the 'A' operation help for more details regarding importing after edit.

Mass-Conversion by extraction
-----------------------------
A subdirectory 'MEDIA' is created, where content, with a known
format is automatically placed, except doom image format as it
lacks a header. See the 'C' operation help for known types.
Adding, editing or removing entries in this folder has no effect
on the 'A' operation. A file in this directory are also present
in its WAD format in the parent directory.
You are free to delete the directory at any time.");
                    break;
                case 'C':
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

C operation
-----------

The C operation converts data from the wad file to a common format.
This conversion is unidirectional

Instead of supplying a wad file and a directory as arguments,
supply the file name and the output file as arguments.

The output file is optional. If not specified. The original name is
used and the matching extension is appended to the name.
WAD Images have no header, a file is assumed to be an image, if it does
not start with any known header.

Known formats:

Converted:
Doom Audio -> WAV (11 KHz)
Doom MUS   -> MID
Doom image -> PNG

Copied 'as-is', if header found:
MP3,WAV,IT,XM,MID

To convert multiple entries, see the 'E' argument help.
The 'E' operation will convert all data, if it finds a matching header.
It is a great way to extract all music files from a WAD at once.");
                    break;
                case 'I':
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

I operation
-----------

The I operation displays all items in the WAD dictionary
This CSV format is used:
<NAME>;<FILENAME>;<OFFSET>;<SIZE>;<TYPE>;<HASH>

NAME      - Name of the entry in the WAD file
FILENAME  - Filename that would be used for extraction
OFFSET    - Offset in bytes of the data in the WAD
SIZE      - Size of the data
TYPE      - Assumed data type from header
HASH      - SHA1 hash of data

The first line just contains the string IWAD or PWAD,
the second line contains the column headers");
                    break;
                default:
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

WADex <operation> <WADfile> [Directory]

WADfile     required WADfile to operate on
operation   Action to perform
            E - Extract all resources to 'Directory' path
            A - Assemble WAD from 'Directory' path
            C - Convert a WAD image to png (or mus to mid)
            I - List resources ('Directory' not to be supplied)
Directory   Directory for 'A' and 'E' operation

Specify only the operation parameter to get specific help");
                    break;
            }
        }

        /// <summary>
        /// Writes a colored message to stderr
        /// </summary>
        /// <param name="C">Color</param>
        /// <param name="Message">Message</param>
        /// <param name="args">Argument (for string.Format)</param>
        public static void Log(ConsoleColor C, string Message, params object[] args)
        {
            ConsoleColor CC = Console.ForegroundColor;
            Console.ForegroundColor = C;
            Console.Error.WriteLine(Message, args);
            Console.ForegroundColor = CC;
        }
    }
}
