using System;
using Tiger;

namespace DestinyMusicViewer
{
    public class DestMusViewer
    {
        public uint PkgId;
        public uint BnkIndex;
        public int SoundCount;

        public DestMusViewer(uint bnkIndex)
        {
            BnkIndex = bnkIndex;
        }

        static string LittleEndian(uint number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            string retval = "";
            foreach (byte b in bytes)
                retval += b.ToString("X2");
            return retval;
        }

        public bool GetBnkInfo(string PackagesPath)
        {
            Extractor extractor = new Extractor(PackagesPath, Tiger.LoggerLevels.HighVerbouse);

            byte[] BnkData = extractor.extract_entry_data(PkgId, (int)BnkIndex).data;

            return true;
        }
    }
}
