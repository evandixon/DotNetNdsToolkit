using DotNetNdsToolkit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NdsToolkitConsole
{
    class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("Usage: NdsToolkitConsole <Input> <Output> [--datapath [data]]");
            Console.WriteLine("Input can be a file or a directory, as long as the output is the other");
        }
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            var filename = args[0];
            var dir = args[1];
            string dataOverride = null;

            if (!Path.IsPathRooted(filename))
            {
                filename = Path.Combine(Directory.GetCurrentDirectory(), filename);
            }

            if (!Path.IsPathRooted(dir))
            {
                dir = Path.Combine(Directory.GetCurrentDirectory(), dir);
            }

            for (int i = 2; i < args.Length; i += 1)
            {
                switch (args[i])
                {
                    case "--datapath":
                        if (i < args.Length - 1)
                        {
                            dataOverride = args[i + 1];
                        }
                        break;
                }
            }

            using var file = await NdsRom.LoadFromFile(filename);
            if (!string.IsNullOrEmpty(dataOverride))
            {
                file.FileSystem.DataPath = dataOverride;
            }

            if (File.Exists(filename))
            {
                await file.Unpack(dir);
            }
            else if (Directory.Exists(filename))
            {
                await file.Save(dir);
            }
        }
    }
}