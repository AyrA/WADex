using System;
using System.Collections.Generic;
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
                        catch(Exception ex)
                        {
                            WF = null;
                            Log(ConsoleColor.Red, "Error: {0}", ex.Message);
                            return;
                        }
                        Console.WriteLine(WF.Type.ToString());
                        foreach (WADentry e in WF.Entries)
                        {
                            Console.WriteLine("{0};{1};{2};{3};{4}", e.Name, e.SafeName, e.Offset, e.Length, e.Hash);
                        }
                    }
                    else
                    {
                        Log(ConsoleColor.Red, "File not found");
                    }
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
                    default:
                        Log(ConsoleColor.Red, "Invalid Operation: {0}", args[0]);
                        break;
                }
            }
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
- recalculating SHA hashes (the index file is not updated)
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
A operation. The file can be edited if you make changes to the
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
If you add a 'Sprite'-type resource, then add it between the matching virtual
entries in the index file.

See the 'A' operation help for more details regarding importing after edit.
");
                    break;
                case 'I':
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

I operation
-----------

The I operation displays all items in the WAD dictionary
This CSV format is used:
<NAME>;<FILENAME>;<OFFSET>;<SIZE>;<HASH>

NAME      - Name of the entry in the WAD file
FILENAME  - Filename that would be used for extraction
OFFSET    - Offset in bytes of the data in the WAD
SIZE      - Size of the data
HASH      - SHA1 hash of data

The first line just contains the string IWAD or PWAD");
                    break;
                default:
                    Console.WriteLine(@"WAD extractor and assembler by AyrA

WADex <operation> <WADfile> [Directory]

WADfile     required WADfile to operate on
operation   Action to perform
            E - Extract all resources to 'Directory' path
            A - Assemble WAD from 'Directory' path
            I - List resources ('Directory' not to be supplied)
Directory   Directory for 'A' and 'E' operation

Specify only the operation parameter to get specific help
");
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
