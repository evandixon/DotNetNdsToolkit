namespace DotNetNdsToolkit.Subtypes
{
    /// <summary>
    /// A single entry in the FAT
    /// </summary>
    public struct FileAllocationEntry
    {
        public FileAllocationEntry(int offset, int endAddress)
        {
            Offset = offset;
            EndAddress = endAddress;
        }

        public int Offset { get; set; }
        public int EndAddress { get; set; }
        public int Length => EndAddress - Offset;
    }
}
