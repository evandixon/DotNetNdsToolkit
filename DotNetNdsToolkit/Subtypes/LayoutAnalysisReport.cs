using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetNdsToolkit.Subtypes
{
    public class LayoutAnalysisReport
    {
        public LayoutAnalysisReport()
        {
            Ranges = new Dictionary<Range, string>();
        }

        /// <summary>
        /// The address ranges of the ROM. Key: range; Value: name
        /// </summary>
        public Dictionary<Range, string> Ranges { get; set; }

        /// <summary>
        /// Consolidates consecutive ranges of the same category
        /// </summary>
        public void CollapseRanges()
        {
            var newRanges = new Dictionary<Range, string>();
            foreach (var item in Ranges.Where(x => x.Key.Length > 0).OrderBy(x => x.Key.Start).GroupBy(x => x.Value, x => x.Key))
            {
                // Get a set of all Ranges in the same category, ordered by the start address
                var ranges = item.ToList();

                // Collapse consecutive ranges
                var currentRange = ranges.First();
                if (ranges.Count > 1)
                {
                    for (int i = 1; i < ranges.Count; i += 1)
                    {
                        if (currentRange.End + 1 == ranges[i].Start)
                        {
                            // This range is consecutive
                            currentRange.Length += currentRange.Length;
                        }
                        else
                        {
                            // This range is separate
                            newRanges.Add(currentRange, item.Key);
                            currentRange = ranges[i];
                        }
                    }
                }
                else
                {
                    newRanges.Add(currentRange, item.Key);
                }
            }
            Ranges = newRanges;
        }

        public string GenerateCSV()
        {
            CollapseRanges();

            var report = new StringBuilder();
            report.AppendLine("Section,Start Address (decimal),End Address (decimal),Length (decimal),Start Address (hex),End Address (hex), Length (hex)");

            var ranges = Ranges.OrderBy(x => x.Key.Start).ToList();
            var currentRange = ranges.First();
            report.AppendLine($"{currentRange.Value},{currentRange.Key.Start},{currentRange.Key.End},{currentRange.Key.Length},{currentRange.Key.Start.ToString("X")},{currentRange.Key.End.ToString("X")},{currentRange.Key.Length.ToString("X")}");
            for (int i = 1; i < ranges.Count; i += 1)
            {
                if (currentRange.Key.End + 1 < ranges[i].Key.Start)
                {
                    // There's some unknown parts between the previous one and this one
                    var section = Properties.Resources.NdsRom_Analysis_UnknownSection;
                    var start = currentRange.Key.End + 1;
                    var length = ranges[i].Key.Start - start;
                    var end = start + length - 1;
                    report.AppendLine($"{section},{start},{end},{length},{start.ToString("X")},{end.ToString("X")},{length.ToString("X")}");
                }
                currentRange = ranges[i];
                report.AppendLine($"{currentRange.Value},{currentRange.Key.Start},{currentRange.Key.End},{currentRange.Key.Length},{currentRange.Key.Start.ToString("X")},{currentRange.Key.End.ToString("X")},{currentRange.Key.Length.ToString("X")}");
            }

            return report.ToString();
        }
    }
}
