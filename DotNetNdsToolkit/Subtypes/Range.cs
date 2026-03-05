namespace DotNetNdsToolkit.Subtypes
{
    public struct Range
    {
        public int Start { get; set; }

        public int Length { get; set; }

        public int End
        {
            get
            {
                return Start + Length - 1;
            }
            set
            {
                Length = (value - Start) + 1;
            }
        }
    }
}
