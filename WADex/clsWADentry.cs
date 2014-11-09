namespace WADex
{
    /// <summary>
    /// Represents an entry in the WAD file
    /// </summary>
    public class WADentry
    {
        /// <summary>
        /// Name of the entry
        /// </summary>
        public string Name
        { get; private set; }
        /// <summary>
        /// File name to use for export (filters invalid chars)
        /// </summary>
        public string SafeName
        {
            get
            {
                string Temp = Name;
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    Temp = Temp.Replace(c, '_');
                }
                return Temp;
            }
        }
        /// <summary>
        /// offset in WAD where data starts
        /// </summary>
        public int Offset
        { get; private set; }
        /// <summary>
        /// number of bytes in Data
        /// </summary>
        public int Length
        {
            get
            {
                if (!Virtual)
                {
                    return Data.Length;
                }
                return 0;
            }
        }
        /// <summary>
        /// Data itself
        /// </summary>
        public byte[] Data
        { get; private set; }
        /// <summary>
        /// True, if virtual (no Data, no Offset)
        /// </summary>
        public bool Virtual
        {
            get
            {
                return Data == null || Data.Length == 0;
            }
        }
        /// <summary>
        /// Gets the SHA1 hash to compare Data of entries
        /// </summary>
        public string Hash
        { get; private set; }

        /// <summary>
        /// Creates a WAD entry
        /// </summary>
        /// <param name="Name">Name of entry</param>
        /// <param name="Offset">Offset, where data starts</param>
        /// <param name="Data">Data</param>
        public WADentry(string Name,int Offset, byte[] Data)
        {
            this.Name = Name;
            this.Offset = Offset;
            this.Data = Data;
            if (Data != null)
            {
                Hash = WADfile.getHash(Data);
            }
            else
            {
                Hash = WADfile.getHash(new byte[0]);
            }
        }

        /// <summary>
        /// Tests if two WADentry instances have the same Data
        /// </summary>
        /// <param name="obj">WADentry</param>
        /// <returns>true, if equal</returns>
        public override bool Equals(object obj)
        {
            if(obj is WADentry)
            {
                return ((WADentry)obj).Hash == Hash;
            }
            return false;
        }

        /// <summary>
        /// Basically HashCode of the Hash string
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        /// <summary>
        /// String representation of this entry
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("WADentry: {0}|{1}", Name, Hash);
        }
    }
}
