using System;

namespace SkyEditor.NdsToolkit.Subtypes
{
    public struct DirectoryMainTable
    {
        public DirectoryMainTable(byte[] rawData)
        {
            SubTableOffset = BitConverter.ToUInt32(rawData, 0);
            FirstSubTableFileID = BitConverter.ToUInt16(rawData, 4);
            ParentDir = BitConverter.ToUInt16(rawData, 6);
        }

        public UInt32 SubTableOffset { get; set; }
        public UInt16 FirstSubTableFileID { get; set; }
        public UInt16 ParentDir { get; set; }
    }
}
