WADex
=====
Extracts and assembles WAD files from DOOM and similar games.

Command line
------------

    WADex.exe <operation> <WADfile> [Directory]
    
    operation  - Either A, E, I or C
                 A = Assemble (create) WAD from 'Directory'
                 E = Extract WAD to 'Directory'
                 I = Display Info about WAD file as CSV
                 C = Convert a file into a common type
    WADfile    - The file to apply the command to
    Directory  - The directory to use for the command
                 Invalid for I operation

To get specific help, just specify the Command but not other arguments.

Index file format
-----------------
The first line contains the text "PWAD" or "IWAD".
An IWAD is the first and main WAD file to be loaded and
a PWAD is a WAD file that extends the game further.
You can have one IWAD and many PWAD files per game.

All lines below contain the directory listing in the order it appeared in the WAD file.
An entry is either a single term, in which case it represents a virtual entry.
Virtual entries are used to group other entries together
and do not have a file attached (they refer to position 0 in the WAD file).
Extract the original DOOM2.WAD file to see how levels,
sprites and other things are made using virtual entries.
A real entry contains of 3 fields, separated with tab chars.
The first entry is the internal name in the WAD file,
the second entry the file name WADex has extracted the content to.
The third entry is the SHA1 hash of the file.
You can use this to verify if an entry has been changed with any SHA1 tool.

A WAD entry does not needs to be unique,
you can have the same entry multiple times.
In the DOOM2.WAD file, levels have the same entry names.
A WAD entry can be maximum 8 chars in length.
Multiple entries can refer to the same file.
In this case the file is only added once to save storage space.
**The SHA1 hash does not needs updating when changing files or referring other files**

The file is beautified to be easier readable:
The WAD entry is spaced to be 8 chars in length (spaces on left)
The File name is spaced to 12 chars with spaces on the left.
You do not need to keep the spacing at all.
The file name can also exceed 12 chars and you can use paths
relative to the index file location to group resources into folders.
If the SHA1 hash bugs you, you can delete it.

Modes of operation
==================
The application supports 4 modes of operation:

- Assembling
- Extracting
- Information
- Conversion

Assembling
----------
The Application will read the **!INDEX.TXT** from the source directory
and create a WAD file out of it.
All entries are parsed ion the order they appear and stored in the WAD file.
For each file an SHA1 hash is created.
If a hash is identical to a previous one
(therefore the file you want to import is already present)
then only a reference is created.
This saves space on the disk and speeds up execution of Doom itself.
Also it requires less ram when executing.

**Assembling will overwrite the destination WAD file if it exists. Be careful**

Extracting
----------
Extracts all WAD file contents to the specified directory and creates **!INDEX.TXT**.
it checks for identical hashes when extracting and only stores references
whenever possible in the index file.

It tries to use the WAD entry name as file name.
This is not always possible for two reasons

- Entry contains invalid file name chars
- File name already used by previous entry

The first issue is solved by substituting all invalid chars with an underscore (_).
The second issue can happen if you have the same entry in the
WAD multiple times but with different data. This is common for level entries.
In this case an underscore is added to the name with a number starting from 0.
The finally used file name is added to the index file.

Please check that the directory is empty before extracting.
An existing index file is overwritten, but existing data files are not.
Instead names are substituted with the naming rules above.
In other words you create unnecessary long file names and you
orphan older files by deleting their index file.
If you happen to land in this mess,
just delete everything an extract again.

As an addition, extracting will also run the 'C' command for exery extracted file,
which is not an image (or unknown type) and the result is put into a subfolder named 'MEDIA'.
You can freely modify and use the contents of said folder without having any effect
on the wad file or the extracted resources.
It is an easy way to collect all media files from a WAD.
To convert a single file, use the 'C' command

Information
-----------
The information command displays WAD file information in a CSV style.
The first line is simply **IWAD** or **PWAD**.
All other lines are the entries in this format (the header is also present as first entry):

    <NAME>;<FILENAME>;<OFFSET>;<SIZE>;<TYPE>;<HASH>

**NAME** is the name in the WAD file

**FILENAME** is the filename that would be used (without considering existing files)

**OFFSET** is the offset in bytes from the beginning of the WAD file of the data

**SIZE** is the size in bytes of the data

**TYPE** is the assumed file type from its header

**HASH** is the SHA1 hash for the data

For virtual entries OFFSET and SIZE are both 0, also TYPE is set to "VIRTUAL"

Conversion
----------
Some entries can be converted to more usable file types.
This conversion is usually unidirectional.
Some entries do not need to be converted,
as they might already be in a usable format (for example an mp3 file)

Known file types to convert:

- Doom Sample -> Results in wav file
- Doom MUS -> Results in midi file
- Doom Image -> Results in PNG image

Known file types to detect and extract:

WAV, MP3, OGG, MID, IT, XM

Messages
========
messages showing the process are sent to stderr,
regular output (especially for I command) is sent to stdout.
