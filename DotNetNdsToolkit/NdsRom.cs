using DotNetNdsToolkit.Subtypes;
using SkyEditor.IO.Binary;
using SkyEditor.IO.FileSystem;
using SkyEditor.Utilities.AsyncFor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Range = DotNetNdsToolkit.Subtypes.Range;

namespace DotNetNdsToolkit
{
    /// <summary>
    /// A ROM for the Nintendo DS.
    /// Use <see cref="LoadFromFile(IReadOnlyBinaryDataAccessor)"/> or <see cref="LoadFromDirectory(string, IFileSystem)"/> to create an instance.
    /// Use <see cref="NdsRom.FileSystem"/> to interact with the files inside, <see cref="NdsRom.Unpack(string)"/> to extract files, or <see cref="NdsRom.Save(string)"/> to build a new ROM from any changes that have been made.
    /// </summary>
    public class NdsRom : IDisposable
    {
        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="data">The raw data of the ROM. Create a <see cref="BinaryFile"/> containing your data if you're unsure what to provide.</param>
        public static async Task<NdsRom> LoadFromFile(IReadOnlyBinaryDataAccessor data, bool disposeData = false)
        {
            var virtualFileSystem = PhysicalFileSystem.Instance;
            var virtualPath = Path.Combine(Path.GetTempPath(), "SkyEditor.IO.NDS", Guid.NewGuid().ToString());

            var rom = new NdsRom(data, disposeData, virtualPath, virtualFileSystem, true);

            await rom.LoadRomHeader().ConfigureAwait(false);

            return rom;
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="filename">Path to the file to load</param>
        /// <param name="fileSystem">File system containing the file to load. Use <see cref="LoadFromDirectory(string)"/> instead if you want your OS's file system.</param>
        public static async Task<NdsRom> LoadFromFile(string filename, IFileSystem fileSystem)
        {
            var binaryFile = new BinaryFile(filename, fileSystem);
            return await LoadFromFile(binaryFile, disposeData: true).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="filename">Path to the file to load</param>
        public static async Task<NdsRom> LoadFromFile(string filename)
        {
            return await LoadFromFile(filename, PhysicalFileSystem.Instance).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="rawData">The raw binary data that was in the file to load</param>
        public static async Task<NdsRom> LoadFromFile(byte[] rawData)
        {
            var binaryFile = new BinaryFile(rawData);
            return await LoadFromFile(binaryFile, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="rawData">The raw binary data that was in the file to load</param>
        public static async Task<NdsRom> LoadFromFile(Memory<byte> rawData)
        {
            var binaryFile = new BinaryFile(rawData);
            return await LoadFromFile(binaryFile, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="rawData">A stream containing the data to load</param>
        /// <param name="disposeData">Whether to dispose <paramref name="rawData"/> when the <see cref="NdsRom"/> is disposed.</param>
        public static async Task<NdsRom> LoadFromFile(Stream rawData, bool disposeData = false)
        {
            var binaryFile = new BinaryFile(rawData);
            return await LoadFromFile(binaryFile, disposeData).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from a file
        /// </summary>
        /// <param name="rawData">A memory mapped file containing the data to load.</param>
        /// <param name="fileLength">Length of <paramref name="rawData"/>, in bytes.</param>
        /// <param name="disposeData">Whether to dispose <paramref name="rawData"/> when the <see cref="NdsRom"/> is disposed.</param>
        public static async Task<NdsRom> LoadFromFile(MemoryMappedFile rawData, int fileLength, bool disposeData = false)
        {
            var binaryFile = new BinaryFile(rawData, fileLength);
            return await LoadFromFile(binaryFile, disposeData).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a ROM from an already-extracted ROM.
        /// </summary>
        /// <param name="directory">Directory containing the extracted files</param>
        /// <param name="fileSystem">File system containing <paramref name="directory"/>. If you want your OS's file system, use <see cref="LoadFromDirectory(string)"/> instead.</param>
        public static NdsRom LoadFromDirectory(string directory, IFileSystem fileSystem)
        {
            return new NdsRom(null, false, directory, fileSystem, false);
        }

        /// <summary>
        /// Loads a ROM from an already-extracted ROM.
        /// </summary>
        /// <param name="directory">Directory containing the extracted files</param>
        public static NdsRom LoadFromDirectory(string directory)
        {
            return LoadFromDirectory(directory, PhysicalFileSystem.Instance);
        }

        /// <summary>
        /// Performs basic analysis to determine whether the provided data is likely a Nintendo DS ROM.
        /// </summary>
        public static async Task<bool> IsOfType(IReadOnlyBinaryDataAccessor data)
        {
            return data.Length > 0x15D && await data.ReadByteAsync(0x15C) == 0x56 && await data.ReadByteAsync(0x15D) == 0xCF;
        }

        protected NdsRom(IReadOnlyBinaryDataAccessor? data, bool disposeData, string virtualPath, IFileSystem virtualFileSystem, bool disposeVirtualPath)
        {
            RawData = data;
            this.disposeData = disposeData;
            this.disposeVirtualPath = disposeVirtualPath;
            FileSystem = new NdsFileSystem(this, virtualPath, virtualFileSystem, disposeVirtualPath);
        }

        private readonly bool disposeData;
        private readonly bool disposeVirtualPath;

        /// <summary>
        /// The underlying data
        /// </summary>
        public IReadOnlyBinaryDataAccessor? RawData { get; protected set; }

        /// <summary>
        /// The raw ROM header
        /// </summary>
        public NdsHeader? Header { get; protected set; }

        /// <summary>
        /// The raw ARM 9 overlay table.
        /// If you're looking for ARM 9 overlay data, use <see cref="FileSystem"/> and browse files in "/overlay".
        /// </summary>
        public IReadOnlyList<OverlayTableEntry> Arm9OverlayTable { get; protected set; } = [];

        /// <summary>
        /// The raw ARM 7 overlay table.
        /// If you're looking for ARM 9 overlay data, use <see cref="FileSystem"/> and browse files in "/overlay7".
        /// </summary>
        public IReadOnlyList<OverlayTableEntry> Arm7OverlayTable { get; protected set; } = [];

        /// <summary>
        /// The raw file allocation table.
        /// If you're looking for the files themselves, use <see cref="FileSystem"/> and browse files in "/data".
        /// </summary>
        public IReadOnlyList<FileAllocationEntry> FAT { get; protected set; } = [];

        /// <summary>
        /// The raw filename table.
        /// If you're looking for the files themselves, use <see cref="FileSystem"/> and browse files in "/data".
        /// </summary>
        public FilenameTable? FNT { get; protected set; }

        /// <summary>
        /// A developer-friendly way of interacting with the ROM file system.
        /// Changes made here will be reflected when the ROM is saved with <see cref="Save(string)"/>.
        /// </summary>
        public NdsFileSystem FileSystem { get; protected set; }

        /// <summary>
        /// Extracts the files contained within the ROM.
        /// </summary>
        /// <param name="targetDir">Directory in the given I/O provider (<paramref name="provider"/>) to store the extracted files</param>
        /// <param name="provider">The file system to contain the extracted files. Use <see cref="Unpack(string)"/> if you want your OS's file system.</param>
        public async Task Unpack(string targetDir, IFileSystem provider, ProgressReportToken? progressReportToken = null)
        {
            // Get the files
            var files = FileSystem.GetFiles("/", "*", false);

            // Set progress
            var totalFileCount = files.Length;
            var extractedFileCount = 0;
            if (progressReportToken != null)
            {
                progressReportToken.IsIndeterminate = false;
                progressReportToken.IsCompleted = false;
            }

            // Ensure directory exists
            if (!provider.DirectoryExists(targetDir))
            {
                provider.CreateDirectory(targetDir);
            }

            // Extract the files
            var extractionTasks = new List<Task>();
            foreach (var item in files)
            {
                var currentItem = item;
                var currentTask = Task.Run(() =>
                {
                    var dest = Path.Combine(targetDir, currentItem.TrimStart('/'));
                    var destDirectoryName = Path.GetDirectoryName(dest);
                    if (string.IsNullOrEmpty(destDirectoryName))
                    {
                        throw new InvalidOperationException($"Could not get directory name of file '{dest}'");
                    }
                    if (!Directory.Exists(destDirectoryName))
                    {
                        lock (_unpackDirectoryCreateLock)
                        {
                            if (!Directory.Exists(destDirectoryName))
                            {
                                Directory.CreateDirectory(destDirectoryName);
                            }
                        }
                    }
                    provider.WriteAllBytes(dest, FileSystem.ReadAllBytes(currentItem));
                    Interlocked.Increment(ref extractedFileCount);
                    if (progressReportToken != null)
                    {
                        progressReportToken.Progress = extractedFileCount / totalFileCount;
                    }
                });

                extractionTasks.Add(currentTask);
            }
            await Task.WhenAll(extractionTasks);
        }
        private readonly object _unpackDirectoryCreateLock = new();

        /// <summary>
        /// Extracts the files contained within the ROM.
        /// </summary>
        /// <param name="targetDir">Directory to store the extracted files</param>
        public async Task Unpack(string targetDir)
        {
            await Unpack(targetDir, PhysicalFileSystem.Instance).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds a new ROM based on current ROM data and any changes made with <see cref="FileSystem"/>.
        /// </summary>
        /// <param name="filename">Filename of the new ROM</param>
        /// <param name="provider">The file system to contain the new ROM. Use <see cref="Save(string)"/> if you want your OS's file system.</param>
        public async Task Save(string filename, IFileSystem provider)
        {
            var overlay9Alloc = new ConcurrentDictionary<int, byte[]>();
            var overlay7Alloc = new ConcurrentDictionary<int, byte[]>();
            var filesAlloc = new ConcurrentDictionary<int, byte[]>();
            var fileNames = new ConcurrentDictionary<string, int>(); // File names of nitrofs (excluding overlays, so not all entries in filesAlloc have a name)
            var overlay9 = new ConcurrentDictionary<int, OverlayTableEntry>(); // Key = index in table, value = the entry
            var overlay7 = new ConcurrentDictionary<int, OverlayTableEntry>(); // Key = index in table, value = the entry
            var fat = new List<byte>();

            int nextFileOffset = 0;

            // Identify files
            var headerData = new BinaryFile(FileSystem.ReadAllBytes("/header.bin"));
            var header = new NdsHeader(headerData);
            var arm9Bin = FileSystem.ReadAllBytes("/arm9.bin");
            var arm7Bin = FileSystem.ReadAllBytes("/arm7.bin");
            var banner = FileSystem.ReadAllBytes("/banner.bin");
            // - Identify ARM9 overlays
            var overlay9Raw = FileSystem.ReadAllBytes("/y9.bin");
            var arm9For = new AsyncFor();
            await arm9For.RunFor(i =>
            {
                var entry = new OverlayTableEntry(overlay9Raw, i);
                var overlayPath = $"/overlay/overlay_{entry.FileID.ToString().PadLeft(4, '0')}.bin";
                if (FileSystem.FileExists(overlayPath))
                {
                    overlay9Alloc[entry.FileID] = FileSystem.ReadAllBytes(overlayPath);
                }
                overlay9[entry.OverlayID] = entry;
            }, 0, overlay9Raw.Length - 1, 32);

            // - Identify ARM7 overlays
            var overlay7Raw = FileSystem.ReadAllBytes("/y7.bin");
            var arm7For = new AsyncFor();
            await arm7For.RunFor(i =>
            {
                var entry = new OverlayTableEntry(overlay7Raw, i);
                var overlayPath = $"/overlay7/overlay_{entry.FileID.ToString().PadLeft(4, '0')}.bin";
                if (FileSystem.FileExists(overlayPath))
                {
                    var data = FileSystem.ReadAllBytes(overlayPath);
                    overlay7Alloc[entry.FileID] = data;
                }
                overlay7[entry.OverlayID] = entry;
            }, 0, overlay7Raw.Length - 1, 32);

            // - Nitrofs
            var overlay9Max = overlay9.Keys.Count > 0 ? overlay9.Keys.Max() : 0;
            var overlay7Max = overlay7.Keys.Count > 0 ? overlay7.Keys.Max() : 0;
            var files = FileSystem.GetFiles("/data", "*", false);
            var filesFor = new AsyncFor();
            await filesFor.RunFor(i =>
            {
                var data = FileSystem.ReadAllBytes(files[i]);
                var fileID = i + overlay9Max + overlay7Max + 1; // File ID is 1 greater than highest index in overlay9 and overlay7
                filesAlloc[fileID] = data;
                fileNames[files[i]] = fileID;

            }, 0, files.Length - 1);
            // - FNT
            var fntSection = EncodeFNT("/data", fileNames);

            // Calculate total file size
            var totalFileSize = 0x4000; // Header size
            // - Banner
            totalFileSize += header.IconLength;
            // - Arm7 + Arm9 + Padding
            totalFileSize += arm9Bin.Length + arm7Bin.Length + CalculatePaddingSize(arm9Bin.Length) + CalculatePaddingSize(arm7Bin.Length);
            // - Arm9 Overlay (files + overlay table + fat)
            totalFileSize += overlay9Alloc.Values.Select(x => x.Length + CalculatePaddingSize(x.Length) + 32 + 8).Sum(); // File + padding + overlay table entry + fat entry
            // - Arm7 Overlay (files + overlay table + fat)
            totalFileSize += overlay7Alloc.Values.Select(x => x.Length + CalculatePaddingSize(x.Length) + 32 + 8).Sum(); // File + padding + overlay table entry + fat entry
            // - Nitrofs  (files + fat)
            totalFileSize += filesAlloc.Values.Select(x => x.Length + CalculatePaddingSize(x.Length) + 8).Sum(); // File + padding + fat entry
            // - FNT
            totalFileSize += fntSection.Count;

            // Set file size
            // Cartridge size = 128KB * (2 ^ DeviceCapacity)
            // Log Base 2 (Cartridge size / 128KB) = DeviceCapacity
            var deviceCapacity = (byte)Math.Ceiling(Math.Log(Math.Ceiling((double)totalFileSize / (128 * 1024)), 2));
            header.DeviceCapacity = deviceCapacity;

            var newData = new BinaryFile(new byte[(long)(Math.Pow(2, deviceCapacity) * 128 * 1024)]);

            // Header: always at 0x00
            // Note: Will rewrite header later to fix file references
            await newData.WriteAsync(0, headerData.ReadArray());

            // ARM9 Binary: always at 0x4000
            header.Arm9RomOffset = 0x4000;
            if (BitConverter.ToUInt32(arm9Bin, arm9Bin.Length - 5) == 0xDEC00621)
            {
                header.Arm9Size = arm9Bin.Length - 0xC;
            }
            else
            {
                header.Arm9Size = arm9Bin.Length;
            }
            var arm9End = header.Arm9RomOffset + arm9Bin.Length;
            await newData.WriteAsync(header.Arm9RomOffset, arm9Bin);
            nextFileOffset = arm9End + await WritePadding(newData, arm9End, arm9Bin.Length);

            // ARM9 Overlay Table
            // - Write the table
            var overlay9Length = 0;
            header.FileArm9OverlayOffset = nextFileOffset;
            for (int i = 0; i < overlay9.Count; i += 1)
            {
                var bytes = overlay9[i].GetBytes();
                await newData.WriteAsync(header.FileArm9OverlayOffset + 32 * i, bytes);
                overlay9Length += 32;
            }
            header.FileArm9OverlaySize = overlay9Length;
            var overlay9End = header.FileArm9OverlayOffset + overlay9Length;
            nextFileOffset = overlay9End + await WritePadding(newData, overlay9End, overlay9Length);

            // - Write ARM9 Overlay Files
            if (overlay9Alloc.Any())
            {
                nextFileOffset = await WriteFATFiles(newData, overlay9Alloc, 0, nextFileOffset, fat);
            }
            else
            {
                header.FileArm9OverlayOffset = 0;
            }

            // ARM7 Binary
            header.Arm7RomOffset = nextFileOffset;
            header.Arm7Size = arm7Bin.Length;
            var arm7End = header.Arm7RomOffset + arm7Bin.Length;
            await newData.WriteAsync(header.Arm7RomOffset, arm7Bin);
            nextFileOffset = arm7End + await WritePadding(newData, arm7End, arm7Bin.Length);

            // ARM7 Overlay Table
            // - Write the table
            var overlay7Length = 0;
            header.FileArm7OverlayOffset = nextFileOffset;
            for (int i = 0; i < overlay7.Count; i += 1)
            {
                var bytes = overlay7[i].GetBytes();
                await newData.WriteAsync(header.FileArm7OverlayOffset + 32 * i, bytes);
                overlay7Length += bytes.Length;
            }
            header.FileArm7OverlaySize = overlay7Length;

            // - Write ARM7 Overlay Files
            if (overlay7Alloc.Any())
            {
                nextFileOffset = await WriteFATFiles(newData, overlay7Alloc, overlay7Alloc.Keys.Min(), nextFileOffset, fat);
            }
            else
            {
                header.FileArm7OverlayOffset = 0;
            }

            // Write FNT
            header.FilenameTableOffset = nextFileOffset;
            var fntData = fntSection.ToArray();
            header.FilenameTableSize = fntData.Length;
            await newData.WriteAsync(nextFileOffset, fntData);
            nextFileOffset += fntSection.Count + await WritePadding(newData, nextFileOffset + fntSection.Count, fntSection.Count);

            // Write dummy fat, since it's still being made
            // -- Calculate total fat size (fat already contains overlays, just need to add nitrofs files)
            header.FileAllocationTableSize = fat.Count + filesAlloc.Keys.Count * 8;
            header.FileAllocationTableOffset = nextFileOffset;
            await newData.WriteAsync(header.FileAllocationTableOffset, new byte[header.FileAllocationTableSize]);
            nextFileOffset += header.FileAllocationTableSize + await WritePadding(newData, header.FileAllocationTableOffset, header.FileAllocationTableSize);

            // Write banner            
            header.IconOffset = nextFileOffset;
            await newData.WriteAsync(header.IconOffset, banner);
            nextFileOffset += header.IconLength + await WritePadding(newData, header.IconOffset + header.IconLength, header.IconLength);

            // Write Files
            if (filesAlloc.Any())
            {
                nextFileOffset = await WriteFATFiles(newData, filesAlloc, filesAlloc.Keys.Min(), nextFileOffset, fat);
            }

            // Write the actual fat
            await newData.WriteAsync(header.FileAllocationTableOffset, fat.ToArray());

            // Write the updated header
            await newData.WriteAsync(0, headerData.ReadArray());

            provider.WriteAllBytes(filename, await newData.ReadArrayAsync());
        }

        /// <summary>
        /// Builds a new ROM based on current ROM data and any changes made with <see cref="FileSystem"/>.
        /// </summary>
        /// <param name="filename">Filename of the new ROM</param>
        public async Task Save(string filename)
        {
            await Save(filename, PhysicalFileSystem.Instance).ConfigureAwait(false);
        }

        /// <summary>
        /// Analyzes the layout of the sections of the ROM
        /// </summary>
        public LayoutAnalysisReport AnalyzeLayout(bool showPadding = false)
        {
            if (Header == null)
            {
                throw new InvalidOperationException("ROM must be loaded from file to analyze layout");
            }

            var report = new LayoutAnalysisReport();

            // Header
            report.Ranges.Add(new Range { Start = 0, Length = NdsHeader.HeaderLength }, Properties.Resources.NdsRom_Analysis_HeaderSection);

            // Icon
            report.Ranges.Add(new Range { Start = Header.IconOffset, Length = Header.IconLength }, Properties.Resources.NdsRom_Analysis_IconSection);

            // ARM9 binary
            var arm9Length = Header.Arm9Size;
            if (CheckNeedsArm9Footer())
            {
                arm9Length += 0xC;
            }
            report.Ranges.Add(new Range { Start = Header.Arm9RomOffset, Length = arm9Length }, Properties.Resources.NdsRom_Analysis_ARM9Section);

            // ARM7 binary
            report.Ranges.Add(new Range { Start = Header.Arm7RomOffset, Length = Header.Arm7Size }, Properties.Resources.NdsRom_Analysis_ARM7Section);

            // ARM9 overlay table
            report.Ranges.Add(new Range { Start = Header.FileArm9OverlayOffset, Length = Header.FileArm9OverlaySize }, Properties.Resources.NdsRom_Analysis_ARM9OverlaySection);

            // ARM7 overlay table
            report.Ranges.Add(new Range { Start = Header.FileArm7OverlayOffset, Length = Header.FileArm7OverlaySize }, Properties.Resources.NdsRom_Analysis_ARM7OverlaySection);

            // FNT
            report.Ranges.Add(new Range { Start = Header.FilenameTableOffset, Length = Header.FilenameTableSize }, Properties.Resources.NdsRom_Analysis_FNTSection);

            // FAT
            report.Ranges.Add(new Range { Start = Header.FileAllocationTableOffset, Length = Header.FileAllocationTableSize }, Properties.Resources.NdsRom_Analysis_FATSection);

            // Files (includes overlay files)
            if (showPadding)
            {
                foreach (var item in FAT)
                {
                    report.Ranges.Add(new Range { Start = item.Offset, Length = item.Length }, Properties.Resources.NdsRom_Analysis_FileSection);
                }
            }
            else
            {
                report.Ranges.Add(new Range { Start = FAT.Min(x => x.Offset), Length = FAT.Max(x => x.EndAddress) }, Properties.Resources.NdsRom_Analysis_FileSection);
            }

            return report;
        }

        /// <summary>
        /// Reads
        /// </summary>
        private async Task LoadRomHeader()
        {
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to load its header");
            }

            Header = new NdsHeader(RawData.Slice(0, 512));

            var loadingTasks = new List<Task>();

            // Load Arm9 Overlays
            var arm9overlayTask = Task.Run(async () => Arm9OverlayTable = await ParseArm9OverlayTable());
            loadingTasks.Add(arm9overlayTask);

            // Load Arm7 Overlays
            var arm7overlayTask = Task.Run(async () => Arm7OverlayTable = await ParseArm7OverlayTable());
            loadingTasks.Add(arm7overlayTask);

            // Load FAT
            var fatTask = Task.Run(async () => FAT = await ParseFAT());
            loadingTasks.Add(fatTask);

            // Load FNT
            var fntTask = Task.Run(async () => FNT = await ParseFNT());
            loadingTasks.Add(fntTask);

            // Wait for all loading
            await Task.WhenAll(loadingTasks).ConfigureAwait(false);
        }

        private async Task<List<OverlayTableEntry>> ParseArm9OverlayTable()
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to parse Arm 9 Overlay Table");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to parse Arm 9 Overlay Table");
            }

            var output = new List<OverlayTableEntry>();
            for (int i = Header.FileArm9OverlayOffset; i < Header.FileArm9OverlayOffset + Header.FileArm9OverlaySize; i += 32)
            {
                output.Add(new OverlayTableEntry(await RawData.ReadArrayAsync(i, 32)));
            }
            return output;
        }

        private async Task<List<OverlayTableEntry>> ParseArm7OverlayTable()
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to parse Arm 7 Overlay Table");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to parse Arm 7 Overlay Table");
            }

            var output = new List<OverlayTableEntry>();
            for (int i = Header.FileArm7OverlayOffset; i < Header.FileArm7OverlayOffset + Header.FileArm7OverlaySize; i += 32)
            {
                output.Add(new OverlayTableEntry(await RawData.ReadArrayAsync(i, 32)));
            }
            return output;
        }

        private async Task<List<FileAllocationEntry>> ParseFAT()
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to parse FAT Table");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to parse FAT Table");
            }

            var output = new List<FileAllocationEntry>();
            for (int i = Header.FileAllocationTableOffset; i < Header.FileAllocationTableOffset + Header.FileAllocationTableSize; i += 8)
            {
                output.Add(new FileAllocationEntry(await RawData.ReadInt32Async(i), await RawData.ReadInt32Async(i + 4)));
            }
            return output;
        }

        private async Task<FilenameTable> ParseFNT()
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to parse FNT Table");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to parse FNT Table");
            }

            // Read the raw structures
            var root = new DirectoryMainTable(await RawData.ReadArrayAsync(Header.FilenameTableOffset, 8));
            var rootDirectories = new List<DirectoryMainTable>();

            // - In the root directory only, ParentDir means the number of directories
            for (int i = 1; i < root.ParentDir; i += 1)
            {
                var offset = Header.FilenameTableOffset + i * 8;
                rootDirectories.Add(new DirectoryMainTable(await RawData.ReadArrayAsync(offset, 8)));
            }

            // Build the filename table
            var output = new FilenameTable
            {
                Name = "data"
            };
            await BuildFNTFromROM(output, root, rootDirectories);
            return output;
        }

        private async Task<List<FNTSubTable>> ReadFNTSubTable(uint rootSubTableOffset, ushort parentFileID)
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to parse FNT Sub Table");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to parse FNT Sub Table");
            }

            var subTables = new List<FNTSubTable>();
            var offset = rootSubTableOffset + Header.FilenameTableOffset;
            var length = await RawData.ReadByteAsync(offset);
            while (length > 0)
            {
                if (length > 128)
                {
                    // Directory
                    var name = await RawData.ReadStringAsync(offset + 1, length & 0x7F, Encoding.ASCII);
                    var subDirID = await RawData.ReadUInt16Async(offset + 1 + (length & 0x7F));
                    subTables.Add(new FNTSubTable { Length = length, Name = name, SubDirectoryID = subDirID });
                    offset += (length & 0x7F) + 1 + 2;
                }
                else if (length < 128)
                {
                    // File
                    var name = await RawData.ReadStringAsync(offset + 1, length, Encoding.ASCII);
                    subTables.Add(new FNTSubTable { Length = length, Name = name, ParentFileID = parentFileID });
                    parentFileID += 1;
                    offset += length + 1;
                }
                else
                {
                    throw new FormatException($"Subtable length of 0x80 is not supported and likely invalid.  Root subtable offset: {rootSubTableOffset}");
                }

                length = await RawData.ReadByteAsync(offset);
            }
            return subTables;
        }

        private async Task BuildFNTFromROM(FilenameTable parentFNT, DirectoryMainTable root, List<DirectoryMainTable> directories)
        {
            foreach (var item in await ReadFNTSubTable(root.SubTableOffset, root.FirstSubTableFileID))
            {
                var child = new FilenameTable { Name = item.Name };
                parentFNT.Children.Add(child);
                if (item.Length > 128)
                {
                    // Directory
                    await BuildFNTFromROM(child, directories[(item.SubDirectoryID & 0x0FFF) - 1], directories);
                }
                else
                {
                    // File
                    child.FileIndex = item.ParentFileID;
                }
            }
        }

        /// <summary>
        /// Builds a filename table from current files, including the shadow directory
        /// </summary>
        /// <param name="path">Path of the node from which to build the FNT</param>
        /// <param name="filenames">Dicitonary matching paths to file indexes. </param>
        private FilenameTable BuildCurrentFNTChild(string path, IDictionary<string, int> filenames, ref UInt16 directoryCount, ref int fileCount)
        {
            var table = new FilenameTable
            {
                Name = Path.GetFileName(path)
            };

            if (FileSystem.FileExists(path))
            {
                table.FileIndex = filenames[path];
                fileCount += 1;
            }
            else // Assume directory exists
            {
                table.DirectoryID = (UInt16)(directoryCount | 0xF000);
                directoryCount += 1;
                foreach (var item in FileSystem.GetDirectories(path, true))
                {
                    table.Children.Add(BuildCurrentFNTChild(item, filenames, ref directoryCount, ref fileCount));
                }
                foreach (var item in FileSystem.GetFiles(path, "*", true))
                {
                    var child = new FilenameTable
                    {
                        Name = Path.GetFileName(item),
                        FileIndex = filenames[item]
                    };
                    
                    table.Children.Add(child);
                    fileCount += 1;
                }
            }
            return table;
        }

        private int? GetFirstFNTFileID(FilenameTable table)
        {
            var firstFileID = table.Children.FirstOrDefault(x => !x.IsDirectory)?.FileIndex;
            if (firstFileID.HasValue)
            {
                return firstFileID;
            }
            else
            {
                foreach (var item in table.Children)
                {
                    firstFileID = GetFirstFNTFileID(item);
                    if (firstFileID.HasValue)
                    {
                        return firstFileID;
                    }
                    // Otherwise, keep looking
                }

                // Couldn't find a file
                return null;
            }
        }

        /// <summary>
        /// Gets the binary representation of the given filename table
        /// </summary>
        protected List<byte> EncodeFNT(string path, IDictionary<string, int> filenames)
        {
            // Generate the FNT
            UInt16 directoryCount = 0;
            int fileCount = 0;
            var table = BuildCurrentFNTChild(path, filenames, ref directoryCount, ref fileCount);

            // Encode the FNT
            var numberTablesWritten = 0;
            var nextSubDirOffset = (directoryCount) * 8;
            var tables = new List<byte>(nextSubDirOffset);
            var subTables = new List<byte>();
            
            numberTablesWritten += 1;

            // Write children
            // Parent dir is directoryCount because of special behavior in the root node.
            WriteFNTDirectory(tables, subTables, table, directoryCount, ref nextSubDirOffset);

            // Concat tables
            tables.AddRange(subTables);

            return tables;
        }

        private void WriteFNTDirectory(List<byte> tables, List<byte> subTables, FilenameTable table, UInt16 parentDir, ref int nextSubDirOffset)
        {
            // Current directory info
            tables.AddRange(BitConverter.GetBytes(nextSubDirOffset));
            tables.AddRange(BitConverter.GetBytes((UInt16)(GetFirstFNTFileID(table) ?? -1)));
            tables.AddRange(BitConverter.GetBytes((UInt16)parentDir));

            // Write children info
            foreach (var item in table.Children.Where(x => !x.IsDirectory))
            {
                byte filenameLength = (byte)(Math.Min(item.Name.Length, 127));
                subTables.Add(filenameLength);
                subTables.AddRange(Encoding.ASCII.GetBytes(item.Name).Take(127));
                nextSubDirOffset += filenameLength + 1;
            }

            foreach (var item in table.Children.Where(x => x.IsDirectory))
            {
                byte filenameLength = (byte)(Math.Min(item.Name.Length, 127));
                subTables.Add((byte)(filenameLength | 0x80)); // Set the directory flag
                subTables.AddRange(Encoding.ASCII.GetBytes(item.Name).Take(127));
                nextSubDirOffset += filenameLength + 1;

                subTables.AddRange(BitConverter.GetBytes((UInt16)(item.DirectoryID))); // Sub-directory ID
                nextSubDirOffset += 2;
            }

            subTables.Add(0);
            nextSubDirOffset += 1;

            // Write childrens' children
            foreach (var item in table.Children.Where(x => x.IsDirectory))
            {
                WriteFNTDirectory(tables, subTables, item, table.DirectoryID, ref nextSubDirOffset);
            }
        }

        /// <summary>
        /// Determines whether or not an additional 0xC of the ARM9 binary is needed
        /// </summary>
        public bool CheckNeedsArm9Footer()
        {
            if (Header == null)
            {
                throw new InvalidOperationException("Header must be loaded to analyze ARM 9 footer requirement");
            }
            if (RawData == null)
            {
                throw new InvalidOperationException("ROM must have been loaded from file to analyze ARM 9 footer requirement");
            }

            return RawData.ReadUInt32(Header.Arm9RomOffset + Header.Arm9Size) == 0xDEC00621;
        }


        /// <summary>
        /// Calculates the padding size of a file
        /// </summary>
        /// <param name="fileLength">Length of the file</param>
        /// <param name="blockSize">Length of the block</param>
        /// <returns>Size of the padding</returns>
        private int CalculatePaddingSize(int fileLength, int blockSize = 0x200)
        {
            int paddingLength = blockSize - (fileLength % blockSize);
            if (paddingLength == blockSize)
            {
                paddingLength = 0;
            }
            return paddingLength;
        }

        /// <summary>
        /// Writes padding for the file
        /// </summary>
        /// <param name="index">The offset at which to write padding</param>
        /// <param name="fileLength">Length of the file to pad</param>
        /// <param name="blockSize">The current block size. Defaults to 0x200</param>
        /// <returns>The length in bytes of the padding written</returns>
        private async Task<int> WritePadding(IWriteOnlyBinaryDataAccessor data, long index, int fileLength, int blockSize = 0x200)
        {
            var paddingLength = CalculatePaddingSize(fileLength, blockSize);
            var padding = new byte[paddingLength];
            Array.Fill<byte>(padding, 0xFF);
            await data.WriteAsync(index, padding);
            return paddingLength;
        }

        /// <summary>
        /// Writes files to the ROM and updates the given raw FAT
        /// </summary>
        /// <param name="filesAlloc">Data to be written</param>
        /// <param name="nextFileOffset">Offset to write the data to</param>
        /// <param name="fat">The raw file allocation table. This will be updated as files are written</param>
        /// <returns>The updated next file offset</returns>
        private async Task<int> WriteFATFiles(IWriteOnlyBinaryDataAccessor data, ConcurrentDictionary<int, byte[]> filesAlloc, int fileIdStart, int nextFileOffset, List<byte> fat)
        {
            for (int i = fileIdStart; i <= filesAlloc.Keys.Max(); i += 1)
            {
                if (filesAlloc.ContainsKey(i))
                {
                    var fileData = filesAlloc[i];

                    await data.WriteAsync(nextFileOffset, fileData); // Write data
                    var paddingLength = await WritePadding(data, nextFileOffset + fileData.Length, fileData.Length);

                    fat.AddRange(BitConverter.GetBytes(nextFileOffset)); // File start index
                    fat.AddRange(BitConverter.GetBytes(nextFileOffset + fileData.Length)); // File end index
                    nextFileOffset += fileData.Length + paddingLength;
                }
                else
                {
                    fat.AddRange(Enumerable.Repeat<byte>(0, 8));
                }
            }
            return nextFileOffset;
        }

        public virtual void Dispose()
        {            
            if (disposeData && RawData is IDisposable rawDataDisposable)
            {
                rawDataDisposable.Dispose();
            }
            if (disposeVirtualPath)
            {
                FileSystem.Dispose();
            }
        }
    }
}
