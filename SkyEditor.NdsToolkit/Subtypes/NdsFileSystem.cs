using SkyEditor.IO.Binary;
using SkyEditor.IO.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SkyEditor.NdsToolkit.Subtypes
{
    public class NdsFileSystem : IFileSystem, IDisposable
    {
        public NdsFileSystem(NdsRom rom, string virtualPath, IFileSystem virtualFileSystem, bool disposeVirtualPath)
        {
            this.rom = rom ?? throw new ArgumentNullException(nameof(rom));
            this.virtualPath = virtualPath ?? throw new ArgumentNullException(nameof(virtualPath));
            this.virtualFileSystem = virtualFileSystem ?? throw new ArgumentNullException(nameof(virtualFileSystem));
            this.disposeVirtualPath = disposeVirtualPath;
        }

        protected readonly NdsRom rom;

        /// <summary>
        /// The I/O provider used for the virtual file system (aka the staging area used to store changes that have not been saved)
        /// </summary>
        protected readonly IFileSystem? virtualFileSystem;

        /// <summary>
        /// Path in the current I/O provider where temporary files are stored
        /// </summary>
        protected readonly string virtualPath;

        /// <summary>
        /// Whether or not to delete <see cref="VirtualPath"/> on delete
        /// </summary>
        protected bool disposeVirtualPath;

        /// <summary>
        /// Keeps track of files that have been logically deleted
        /// </summary>
        protected List<string> BlacklistedPaths { get; } = [];

        /// <summary>
        /// Path where the NitroFS files live. Useful when compatibility with external ROM extractors is needed.
        /// Leave this alone if you're using exclusively this library.
        /// </summary>
        public string DataPath { get; set; } = "data";

        public string WorkingDirectory
        {
            get
            {
                var path = new StringBuilder();
                foreach (var item in _workingDirectoryParts)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        path.Append("/");
                        path.Append(item);
                    }
                }
                path.Append("/");
                return path.ToString();
            }
            set
            {
                _workingDirectoryParts = GetPathParts(value);
            }
        }

        private string[] _workingDirectoryParts = [];

        protected string[] GetPathParts(string path)
        {
            var parts = new List<string>();

            path = path.Replace('\\', '/');
            if (!path.StartsWith("/") && !(_workingDirectoryParts.Length == 1 && _workingDirectoryParts[0] == string.Empty))
            {
                parts.AddRange(_workingDirectoryParts);
            }

            foreach (var item in path.TrimStart('/').Split('/'))
            {
                switch (item)
                {
                    case "":
                    case ".":
                        break;
                    case "..":
                        parts.RemoveAt(parts.Count - 1);
                        break;
                    default:
                        parts.Add(item);
                        break;
                }
            }
            if (parts.Count == 0)
            {
                parts.Add(string.Empty);
            }
            return parts.ToArray();
        }

        public void ResetWorkingDirectory()
        {
            this.WorkingDirectory = "/";
        }

        private string FixPath(string path)
        {
            var fixedPath = path.Replace('\\', '/');

            // Apply working directory
            if (fixedPath.StartsWith("/"))
            {
                return fixedPath;
            }
            else
            {
                return Path.Combine(this.WorkingDirectory, path);
            }
        }

        private string GetVirtualPath(string path)
        {
            return Path.Combine(virtualPath, path.TrimStart('/'));
        }

        private FileAllocationEntry? GetFATEntry(string path)
        {
            if (rom.Header == null)
            {
                return null;
            }

            var parts = GetPathParts(path);
            var partLower = parts[0].ToLower();
            switch (partLower)
            {
                case "overlay":
                    int index;
                    if (int.TryParse(parts[1].ToLower().Substring(8, 4), out index))
                    {
                        OverlayTableEntry entry = rom.Arm9OverlayTable.FirstOrDefault(x => x.FileID == index);
                        if (entry != default)
                        {
                            return rom.FAT[entry.FileID];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    break;
                case "overlay7":
                    int index7;
                    if (int.TryParse(parts[1].ToLower().Substring(8, 4), out index7))
                    {
                        OverlayTableEntry entry = rom.Arm7OverlayTable.FirstOrDefault(x => x.FileID == index7);
                        if (entry != default)
                        {
                            return rom.FAT[entry.FileID];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    break;
                case "arm7.bin":
                    return new FileAllocationEntry(rom.Header.Arm7RomOffset, rom.Header.Arm7RomOffset + rom.Header.Arm7Size);
                case "arm9.bin":
                    if (rom.CheckNeedsArm9Footer())
                    {
                        return new FileAllocationEntry(rom.Header.Arm9RomOffset, rom.Header.Arm9RomOffset + rom.Header.Arm9Size + 0xC);
                    }
                    else
                    {
                        return new FileAllocationEntry(rom.Header.Arm9RomOffset, rom.Header.Arm9RomOffset + rom.Header.Arm9Size + 0xC);
                    }
                case "header.bin":
                    return new FileAllocationEntry(0, 0x200);
                case "banner.bin":
                    return new FileAllocationEntry(rom.Header.IconOffset, rom.Header.IconOffset + rom.Header.IconLength);
                case "y7.bin":
                    return new FileAllocationEntry(rom.Header.FileArm7OverlayOffset, rom.Header.FileArm7OverlayOffset + rom.Header.FileArm7OverlaySize);
                case "y9.bin":
                    return new FileAllocationEntry(rom.Header.FileArm9OverlayOffset, rom.Header.FileArm9OverlayOffset + rom.Header.FileArm9OverlaySize);
                default:
                    if (partLower == DataPath)
                    {
                        var currentEntry = rom.FNT;
                        for (int i = 1; i < parts.Length; i += 1)
                        {
                            currentEntry = currentEntry?.Children.FirstOrDefault(x => x.Name.Equals(parts[i], StringComparison.CurrentCultureIgnoreCase));
                        }
                        if (currentEntry != null && !currentEntry.IsDirectory)
                        {
                            return rom.FAT[currentEntry.FileIndex];
                        }
                    }
                    break;
            }

            return null;
        }

        private FileAllocationEntry GetRequiredFATEntry(string path)
        {
            var entry = GetFATEntry(path);
            if (!entry.HasValue)
            {
                throw new FileNotFoundException(string.Format(Properties.Resources.ErrorRomFileNotFound, path), path);
            }
            return entry.Value;
        }

        public long GetFileLength(string filename)
        {
            if (virtualFileSystem != null && virtualFileSystem.FileExists(GetVirtualPath(filename)))
            {
                return virtualFileSystem.GetFileLength(filename);
            }

            return GetRequiredFATEntry(filename).Length;
        }

        public bool FileExists(string filename)
        {
            return (virtualFileSystem != null && virtualFileSystem.FileExists(GetVirtualPath(filename)))
                || GetFATEntry(filename).HasValue;
        }

        private bool DirectoryExists(string[] parts)
        {
            if (parts.Length == 1)
            {
                switch (parts[0].ToLower())
                {
                    case "overlay":
                        return true;
                    case "overlay7":
                        return true;
                    default:
                        return parts[0].Equals(DataPath, StringComparison.CurrentCultureIgnoreCase);
                }
            }
            else if (parts.Length == 0)
            {
                throw new ArgumentException("Argument cannot be empty", nameof(parts));
            }
            else
            {
                if (parts[0].ToLower() == DataPath)
                {
                    var currentEntry = rom.FNT;
                    for (int i = 1; i < parts.Length; i += 1)
                    {
                        var currentPartLower = parts[i].ToLower();
                        currentEntry = currentEntry?.Children.FirstOrDefault(x => x.Name.ToLower() == currentPartLower);
                    }
                    return (currentEntry?.IsDirectory).HasValue;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool DirectoryExists(string path)
        {
            return !BlacklistedPaths.Contains(FixPath(path))
                    &&
                    ((virtualFileSystem != null && virtualFileSystem.DirectoryExists(GetVirtualPath(path)))
                        || DirectoryExists(GetPathParts(path))
                    );
        }

        public void CreateDirectory(string path)
        {
            var fixedPath = FixPath(path);
            if (BlacklistedPaths.Contains(fixedPath))
            {
                BlacklistedPaths.Remove(fixedPath);
            }

            if (!this.DirectoryExists(fixedPath))
            {
                virtualFileSystem?.CreateDirectory(GetVirtualPath(fixedPath));
            }
        }

        private IEnumerable<string> GetFilesFromNode(string pathBase, FilenameTable currentTable, Regex searchPatternRegex, bool topDirectoryOnly)
        {
            var output = new List<string>();
            foreach (var item in currentTable.Children.Where(x => !x.IsDirectory))
            {
                if (searchPatternRegex.IsMatch(item.Name))
                {
                    output.Add(pathBase + "/" + item.Name);
                }
            }
            if (!topDirectoryOnly)
            {
                foreach (var item in currentTable.Children.Where(x => x.IsDirectory))
                {
                    output.AddRange(GetFilesFromNode(pathBase + "/" + item.Name, item, searchPatternRegex, topDirectoryOnly));
                }
            }
            return output;
        }

        public string[] GetFiles(string path, string searchPattern, bool topDirectoryOnly)
        {
            var output = new List<string>();
            var parts = GetPathParts(path);
            var searchPatternRegex = new Regex(GetFileSearchRegex(searchPattern), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            switch (parts[0].ToLower())
            {
                case "":
                    output.Add("/arm7.bin");
                    output.Add("/arm9.bin");
                    output.Add("/header.bin");
                    output.Add("/banner.bin");
                    output.Add("/y7.bin");
                    output.Add("/y9.bin");
                    if (!topDirectoryOnly)
                    {
                        output.AddRange(this.GetFiles("/overlay", searchPattern, topDirectoryOnly));
                        output.AddRange(this.GetFiles("/overlay7", searchPattern, topDirectoryOnly));
                        output.AddRange(this.GetFiles("/" + DataPath, searchPattern, topDirectoryOnly));
                    }
                    return output.ToArray();
                case "overlay":
                    // Original files
                    for (int i = 0; i < rom.Arm9OverlayTable.Count; i += 1)
                    {
                        var overlayPath = $"/overlay/overlay_{rom.Arm9OverlayTable[i].FileID.ToString().PadLeft(4, '0')}.bin";
                        if (searchPatternRegex.IsMatch(Path.GetFileName(overlayPath)))
                        {
                            if (!BlacklistedPaths.Contains(overlayPath))
                            {
                                output.Add(overlayPath);
                            }
                        }
                    }

                    // Apply shadowed files
                    var virtualPath9 = GetVirtualPath(parts[0].ToLower());
                    if (virtualFileSystem != null && virtualFileSystem.DirectoryExists(virtualPath9))
                    {
                        foreach (var item in virtualFileSystem.GetFiles(virtualPath9, "overlay_*.bin", true))
                        {
                            if (searchPatternRegex.IsMatch(Path.GetFileName(item)))
                            {
                                var overlayPath = "/" + Path.GetRelativePath(item, this.virtualPath);
                                if (!BlacklistedPaths.Contains(overlayPath) && !output.Contains(overlayPath))
                                {
                                    output.Add(overlayPath);
                                }
                            }
                        }
                    }
                    return output.ToArray();
                case "overlay7":
                    // Original files
                    for (int i = 0; i < rom.Arm7OverlayTable.Count; i += 1)
                    {
                        var overlayPath = $"/overlay7/overlay_{rom.Arm7OverlayTable[i].FileID.ToString().PadLeft(4, '0')}.bin";
                        if (searchPatternRegex.IsMatch(Path.GetFileName(overlayPath)))
                        {
                            if (!BlacklistedPaths.Contains(overlayPath))
                            {
                                output.Add(overlayPath);
                            }
                        }
                    }

                    // Apply shadowed files
                    var virtualPath7 = GetVirtualPath(parts[0].ToLower());
                    if (virtualFileSystem != null && virtualFileSystem.DirectoryExists(virtualPath7))
                    {
                        foreach (var item in virtualFileSystem.GetFiles(virtualPath7, "overlay_*.bin", true))
                        {
                            if (searchPatternRegex.IsMatch(Path.GetFileName(item)))
                            {
                                var overlayPath = "/" + Path.GetRelativePath(item, this.virtualPath);
                                if (!BlacklistedPaths.Contains(overlayPath) && !output.Contains(overlayPath))
                                {
                                    output.Add(overlayPath);
                                }
                            }
                        }
                    }
                    return output.ToArray();
                default:
                    if (parts[0].ToLower() == DataPath)
                    {
                        // Get the desired directory
                        var currentEntry = rom.FNT;
                        var pathBase = new StringBuilder();
                        pathBase.Append("/" + DataPath);
                        for (int i = 1; i < parts.Length; i += 1)
                        {
                            var partLower = parts[i].ToLower();
                            currentEntry = currentEntry?.Children.Where(x => x.Name.ToLower() == partLower && x.IsDirectory).FirstOrDefault();
                            if (currentEntry == null)
                            {
                                break;
                            }
                            else
                            {
                                pathBase.Append($"/{currentEntry.Name}");
                            }
                        }

                        // Get the files
                        if (currentEntry != null && currentEntry.IsDirectory)
                        {
                            output.AddRange(GetFilesFromNode(pathBase.ToString(), currentEntry, searchPatternRegex, topDirectoryOnly));
                        }

                        // Apply shadowed files
                        var virtualPathData = GetVirtualPath(path);
                        if (virtualFileSystem != null && virtualFileSystem.DirectoryExists(virtualPathData))
                        {
                            foreach (var item in virtualFileSystem.GetFiles(virtualPathData, searchPattern, topDirectoryOnly))
                            {
                                var filePath = "/" + Path.GetRelativePath(virtualPath, item);
                                if (!output.Contains(filePath))
                                {
                                    output.Add(filePath);
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException(string.Format(Properties.Resources.ErrorRomFileNotFound, path), path);
                    }
                    break;
            }
            return output.ToArray();
        }

        public string[] GetDirectories(string path, bool topDirectoryOnly)
        {
            var output = new List<string>();
            var parts = GetPathParts(path);
            switch (parts[0].ToLower())
            {
                case "":
                    output.Add("/" + DataPath);
                    output.Add("/overlay");
                    output.Add("/overlay7");
                    break;
                case "overlay":
                case "overlay7":
                    // Overlays have no child directories
                    break;
                default:
                    if (parts[0].ToLower() == DataPath)
                    {
                        var currentEntry = rom.FNT;
                        for (int i = 1; i < parts.Length; i += 1)
                        {
                            var partLower = parts[i].ToLower();
                            currentEntry = currentEntry?.Children.Where(x => x.Name.ToLower() == partLower && x.IsDirectory).FirstOrDefault();
                        }

                        if (currentEntry != null && currentEntry.IsDirectory)
                        {
                            output.AddRange(currentEntry.Children.Where(x => x.IsDirectory).Select(x => path + "/" + x.Name));
                        }

                        // Apply shadowed files
                        var virtualPathData = GetVirtualPath(path);
                        if (virtualFileSystem != null && virtualFileSystem.DirectoryExists(virtualPathData))
                        {
                            foreach (var item in virtualFileSystem.GetDirectories(virtualPathData, topDirectoryOnly))
                            {
                                var filePath = "/" + Path.GetRelativePath(virtualPath, item);
                                if (!output.Contains(filePath))
                                {
                                    output.Add(filePath);
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException(string.Format(Properties.Resources.ErrorRomFileNotFound, path), path);
                    }
                    break;
            }
            if (!topDirectoryOnly)
            {
                foreach (var item in output)
                {
                    output.AddRange(this.GetDirectories(item, topDirectoryOnly));
                }
            }
            return output.ToArray();
        }

        public byte[] ReadAllBytes(string filename)
        {
            var fixedPath = FixPath(filename);
            if (BlacklistedPaths.Contains(fixedPath))
            {
                throw new FileNotFoundException(string.Format(Properties.Resources.ErrorRomFileNotFound, filename), filename);
            }
            else
            {
                var virtualPath = GetVirtualPath(fixedPath);
                if (virtualFileSystem != null && virtualFileSystem.FileExists(virtualPath))
                {
                    return virtualFileSystem.ReadAllBytes(virtualPath);
                }
                else
                {
                    var entry = GetRequiredFATEntry(filename);
                    if (rom.RawData == null)
                    {
                        throw new InvalidOperationException("ROM must be loaded from file to read a FAT entry");
                    }
                    return rom.RawData.ReadArray(entry.Offset, entry.Length);
                }
            }
        }

        public void CopyFile(string sourceFilename, string destinationFilename)
        {
            this.WriteAllBytes(destinationFilename, this.ReadAllBytes(sourceFilename));
        }

        public void DeleteFile(string filename)
        {
            var fixedPath = FixPath(filename);
            if (!BlacklistedPaths.Contains(fixedPath))
            {
                BlacklistedPaths.Add(fixedPath);
            }

            var virtualPath = GetVirtualPath(filename);
            if (virtualFileSystem != null && virtualFileSystem.FileExists(virtualPath))
            {
                virtualFileSystem.DeleteFile(virtualPath);
            }
        }

        public void DeleteDirectory(string path)
        {
            var fixedPath = FixPath(path);
            if (!BlacklistedPaths.Contains(fixedPath))
            {
                BlacklistedPaths.Add(fixedPath);
            }

            var virtualPath = GetVirtualPath(path);
            if (virtualFileSystem != null && virtualFileSystem.FileExists(virtualPath))
            {
                virtualFileSystem.DeleteFile(virtualPath);
            }
        }

        public string GetTempFilename()
        {
            var path = "/temp/files/" + Guid.NewGuid().ToString();
            this.WriteAllBytes(path, []);
            return path;
        }

        public string GetTempDirectory()
        {
            var path = "/temp/dirs/" + Guid.NewGuid().ToString();
            this.CreateDirectory(path);
            return path;
        }

        public Stream OpenFile(string filename)
        {
            if (virtualFileSystem == null)
            {
                throw new NotSupportedException("Cannot open a file as a stream without an IO provider.");
            }

            var virtualPath = GetVirtualPath(filename);
            var virtualDirectory = Path.GetDirectoryName(virtualPath);
            if (string.IsNullOrEmpty(virtualDirectory))
            {
                throw new InvalidOperationException($"Could not get directory for virtual path '{virtualPath}'");
            }
            if (!virtualFileSystem.DirectoryExists(virtualDirectory))
            {
                virtualFileSystem.CreateDirectory(virtualDirectory);
            }

            var file = virtualFileSystem.OpenFile(virtualPath);
            var entry = GetFATEntry(filename);
            if (entry != null)
            {
                if (rom.RawData == null)
                {
                    throw new InvalidOperationException("ROM must be loaded from file to read a FAT entry");
                }
                var data = rom.RawData.ReadArray(entry.Value.Offset, entry.Value.Length);
                file.Write(data);
                file.Position = 0;
            }

            return file;
        }

        public Stream OpenFileReadOnly(string filename)
        {
            if (virtualFileSystem != null)
            {
                var virtualPath = GetVirtualPath(filename);
                if (virtualFileSystem.FileExists(virtualPath))
                {
                    return virtualFileSystem.OpenFileReadOnly(virtualPath);
                }
            }

            return new MemoryStream(this.ReadAllBytes(filename));
        }

        public Stream OpenFileWriteOnly(string filename)
        {
            if (virtualFileSystem == null)
            {
                throw new NotSupportedException("Cannot open a file as a stream without an IO provider.");
            }

            var virtualPath = GetVirtualPath(filename);
            var virtualDirectory = Path.GetDirectoryName(virtualPath);
            if (string.IsNullOrEmpty(virtualDirectory))
            {
                throw new InvalidOperationException($"Could not get directory for virtual path '{virtualPath}'");
            }
            if (!virtualFileSystem.DirectoryExists(virtualDirectory))
            {
                virtualFileSystem.CreateDirectory(virtualDirectory);
            }

            var file = virtualFileSystem.OpenFile(virtualPath);
            var entry = GetFATEntry(filename);
            if (entry != null)
            {
                if (rom.RawData == null)
                {
                    throw new InvalidOperationException("ROM must be loaded from file to read a FAT entry");
                }
                var data = rom.RawData.ReadArray(entry.Value.Offset, entry.Value.Length);
                file.Write(data);
                file.Position = 0;
            }

            return file;
        }

        /// <summary>
        /// Gets a regular expression for the given search pattern for use with <see cref="GetFiles(string, string, bool)"/>.  Do not provide asterisks.
        /// </summary>
        private static StringBuilder GetFileSearchRegexQuestionMarkOnly(string searchPattern)
        {
            var parts = searchPattern.Split('?');
            var regexString = new StringBuilder();
            foreach (var item in parts)
            {
                regexString.Append(Regex.Escape(item));
                if (item != parts[parts.Length - 1])
                {
                    regexString.Append(".?");
                }
            }
            return regexString;
        }

        /// <summary>
        /// Gets a regular expression for the given search pattern for use with <see cref="GetFiles(string, string, bool)"/>.
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        private static string GetFileSearchRegex(string searchPattern)
        {
            var asteriskParts = searchPattern.Split('*');
            var regexString = new StringBuilder();

            foreach (var part in asteriskParts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    // Asterisk
                    regexString.Append(".*");
                }
                else
                {
                    regexString.Append(GetFileSearchRegexQuestionMarkOnly(part));
                }
            }

            return regexString.ToString();
        }

        public virtual void Dispose()
        {
            if (virtualFileSystem != null && disposeVirtualPath && virtualFileSystem.DirectoryExists(virtualPath))
            {
                virtualFileSystem.DeleteDirectory(virtualPath);
            }
        }
    }
}
