using System;

namespace DotNetNdsToolkit.Subtypes
{
    public struct FNTSubTable
    {
        public byte Length { get; set; }
        public string Name { get; set; }
        public UInt16 SubDirectoryID { get; set; } // Only used for directories
        public UInt16 ParentFileID { get; set; }
        public override string ToString()
        {
            return $"Length: {Length}, Sub-Directory ID: {SubDirectoryID}, Parent File ID: {ParentFileID}, Name: {Name}";
        }
    }
}
