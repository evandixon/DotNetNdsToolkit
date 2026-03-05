using System;
using System.Collections.Generic;

namespace SkyEditor.NdsToolkit.Subtypes
{
    public class FilenameTable
    {
#if NET10_0_OR_GREATER
        public required string Name { get; set; }
#else
        public string Name { get; set; } = default!;
#endif

        public int FileIndex { get; set; } = -1;

        public UInt16 DirectoryID { get; set; }

        public bool IsDirectory => FileIndex < 0;

        public List<FilenameTable> Children { get; set; } = [];

        public override string? ToString()
        {
            return Name;
        }
    }
}
