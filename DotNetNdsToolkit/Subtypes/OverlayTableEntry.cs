using System;
using System.Collections.Generic;

namespace DotNetNdsToolkit.Subtypes
{
    /// <summary>
    /// A single entry in an overlay table
    /// </summary>
    public struct OverlayTableEntry
    {
        public OverlayTableEntry(byte[] rawData, int offset = 0)
        {
            OverlayID = BitConverter.ToInt32(rawData, offset + 0);
            RamAddress = BitConverter.ToInt32(rawData, offset + 4);
            RamSize = BitConverter.ToInt32(rawData, offset + 8);
            BssSize = BitConverter.ToInt32(rawData, offset + 0xC);
            StaticInitStart = BitConverter.ToInt32(rawData, offset + 0x10);
            StaticInitEnd = BitConverter.ToInt32(rawData, offset + 0x14);
            FileID = BitConverter.ToInt32(rawData, offset + 0x18);
        }

        public int OverlayID { get; set; }
        public int RamAddress { get; set; }
        public int RamSize { get; set; }
        public int BssSize { get; set; }
        public int StaticInitStart { get; set; }
        public int StaticInitEnd { get; set; }
        public int FileID { get; set; }

        public byte[] GetBytes()
        {
            var output = new List<byte>();

            output.AddRange(BitConverter.GetBytes(OverlayID));
            output.AddRange(BitConverter.GetBytes(RamAddress));
            output.AddRange(BitConverter.GetBytes(RamSize));
            output.AddRange(BitConverter.GetBytes(BssSize));
            output.AddRange(BitConverter.GetBytes(StaticInitStart));
            output.AddRange(BitConverter.GetBytes(StaticInitEnd));
            output.AddRange(BitConverter.GetBytes(FileID));

            return output.ToArray();
        }

        public static bool operator ==(OverlayTableEntry a, OverlayTableEntry b)
        {
            return a.OverlayID == b.OverlayID && a.RamAddress == b.RamAddress && a.RamSize == b.RamSize && a.BssSize == b.BssSize && a.StaticInitStart == b.StaticInitStart && a.StaticInitEnd == b.StaticInitEnd && a.FileID == b.FileID;
        }

        public static bool operator !=(OverlayTableEntry a, OverlayTableEntry b)
        {
            return a.OverlayID != b.OverlayID || a.RamAddress != b.RamAddress || a.RamSize != b.RamSize || a.BssSize != b.BssSize || a.StaticInitStart != b.StaticInitStart || a.StaticInitEnd != b.StaticInitEnd || a.FileID != b.FileID;
        }

        public override bool Equals(object? obj)
        {
            return obj is OverlayTableEntry && (OverlayTableEntry)obj == this;
        }

        public override int GetHashCode()
        {
            return OverlayID ^ RamAddress ^ RamSize ^ BssSize ^ StaticInitStart ^ StaticInitEnd ^ FileID;
        }
    }
}
