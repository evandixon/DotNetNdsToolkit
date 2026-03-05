namespace SkyEditor.NdsToolkit.Subtypes
{
    /// <summary>
    /// Represents an entry in the overlay table, in addition to the overlay itself
    /// </summary>
    public class Overlay
    {
        public OverlayTableEntry TableEntry { get; set; }
        public byte[]? Data { get; set; }
    }
}
