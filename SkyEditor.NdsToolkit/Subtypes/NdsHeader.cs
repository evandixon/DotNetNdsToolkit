using SkyEditor.IO.Binary;
using System;
using System.Text;

namespace SkyEditor.NdsToolkit.Subtypes
{
    /// <summary>
    /// The NDS header
    /// </summary>
    public class NdsHeader
    {
        public const int HeaderLength = 512;

        public NdsHeader(IReadOnlyBinaryDataAccessor data)
        {
            this.reader = data;
        }
        public NdsHeader(IBinaryDataAccessor data)
        {
            this.reader = data;
            this.writer = data;
        }

        private readonly IReadOnlyBinaryDataAccessor reader;
        private readonly IWriteOnlyBinaryDataAccessor? writer;

        public string GameTitle
        {
            get
            {
                return reader.ReadString(0, 12, Encoding.ASCII);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteString(0, Encoding.ASCII, value.PadRight(12, '\0').Substring(0, 12));
            }
        }

        public string GameCode
        {
            get
            {
                return reader.ReadString(12, 4, Encoding.ASCII);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteString(12, Encoding.ASCII, value.PadRight(4, '\0').Substring(0, 4));
            }
        }

        public string MakerCode
        {
            get
            {
                return reader.ReadString(16, 2, Encoding.ASCII);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteString(16, Encoding.ASCII, value.PadRight(2, '\0').Substring(0, 2));
            }
        }

        public byte UnitCode
        {
            get
            {
                return reader.ReadByte(0x12);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.Write(0x12, value);
            }
        }

        public byte EncryptionSeedSelect
        {
            get
            {
                return reader.ReadByte(0x13);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.Write(0x13, value);
            }
        }

        /// <summary>
        /// The capacity of the cartridge.  Cartridge size = 128KB * (2 ^ DeviceCapacity)
        /// </summary>
        public byte DeviceCapacity
        {
            get
            {
                return reader.ReadByte(0x14);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.Write(0x14, value);
            }
        }

        /// <summary>
        /// Region of the ROM.
        /// (00h=Normal, 80h=China, 40h=Korea)
        /// </summary>
        public byte NdsRegion
        {
            get
            {
                return reader.ReadByte(0x1D);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.Write(0x1D, value);
            }
        }

        public byte RomVersion
        {
            get
            {
                return reader.ReadByte(0x1E);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.Write(0x1E, value);
            }
        }

        //01Fh    1     Autostart (Bit2: Skip "Press Button" after Health and Safety)
        //(Also skips bootmenu, even in Manual mode & even Start pressed)

        public int Arm9RomOffset
        {
            get
            {
                return reader.ReadInt32(0x20);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x20, value);
            }
        }

        public int Arm9EntryAddress
        {
            get
            {
                return reader.ReadInt32(0x24);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x24, value);
            }
        }
        public int Arm9RamAddress
        {
            get
            {
                return reader.ReadInt32(0x28);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x28, value);
            }
        }

        public int Arm9Size
        {
            get
            {
                return reader.ReadInt32(0x2C);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x2C, value);
            }
        }

        public int Arm7RomOffset
        {
            get
            {
                return reader.ReadInt32(0x30);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x30, value);
            }
        }

        public int Arm7EntryAddress
        {
            get
            {
                return reader.ReadInt32(0x34);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x34, value);
            }
        }
        public int Arm7RamAddress
        {
            get
            {
                return reader.ReadInt32(0x38);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x38, value);
            }
        }

        public int Arm7Size
        {
            get
            {
                return reader.ReadInt32(0x3C);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x3C, value);
            }
        }

        public int FilenameTableOffset
        {
            get
            {
                return reader.ReadInt32(0x40);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x40, value);
            }
        }

        public int FilenameTableSize
        {
            get
            {
                return reader.ReadInt32(0x44);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x44, value);
            }
        }

        public int FileAllocationTableOffset
        {
            get
            {
                return reader.ReadInt32(0x48);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x48, value);
            }
        }

        public int FileAllocationTableSize
        {
            get
            {
                return reader.ReadInt32(0x4C);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x4C, value);
            }
        }

        public int FileArm9OverlayOffset
        {
            get
            {
                return reader.ReadInt32(0x50);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x50, value);
            }
        }

        public int FileArm9OverlaySize
        {
            get
            {
                return reader.ReadInt32(0x54);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x54, value);
            }
        }

        public int FileArm7OverlayOffset
        {
            get
            {
                return reader.ReadInt32(0x58);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x58, value);
            }
        }

        public int FileArm7OverlaySize
        {
            get
            {
                return reader.ReadInt32(0x5C);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x5C, value);
            }
        }

        // 060h    4     Port 40001A4h setting for normal commands (usually 00586000h)
        // 064h    4     Port 40001A4h setting for KEY1 commands   (usually 001808F8h)

        public int IconOffset
        {
            get
            {
                return reader.ReadInt32(0x68);
            }
            set
            {
                if (writer == null)
                {
                    throw new InvalidOperationException("This header was not initialized with a data writer.");
                }

                writer.WriteInt32(0x68, value);
            }
        }

        public int IconLength
        {
            get
            {
                return 0x840;
            }
        }

        // 06Ch    2     Secure Area Checksum, CRC-16 of [ [20h]..7FFFh]
        // 06Eh    2     Secure Area Loading Timeout (usually 051Eh)
        // 070h    4     ARM9 Auto Load List RAM Address (?)
        // 074h    4     ARM7 Auto Load List RAM Address (?)
        // 078h    8     Secure Area Disable (by encrypted "NmMdOnly") (usually zero)
        // 080h    4     Total Used ROM size (remaining/unused bytes usually FFh-padded)
        // 084h    4     ROM Header Size (4000h)
        // 088h    38h   Reserved (zero filled)
        // 0C0h    9Ch   Nintendo Logo (compressed bitmap, same as in GBA Headers)
        // 15Ch    2     Nintendo Logo Checksum, CRC-16 of [0C0h-15Bh], fixed CF56h
        // 15Eh    2     Header Checksum, CRC-16 of [000h-15Dh]
        // 160h    4     Debug rom_offset   (0=none) (8000h and up)       ;only if debug
        // 164h    4     Debug size         (0=none) (max 3BFE00h)        ;version with
        // 168h    4     Debug ram_address  (0=none) (2400000h..27BFE00h) ;SIO and 8MB
        // 16Ch    4     Reserved (zero filled) (transferred, and stored, but not used)
        // 170h    90h   Reserved (zero filled) (transferred, but not stored in RAM)

    }
}
