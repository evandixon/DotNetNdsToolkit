using DotNetNdsToolkit;
using SkyEditor.Core.IO;
using SkyEditor.IO.FileSystem;
using System;
using System.IO;

namespace NdsToolkitConsole
{
    class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("Usage: NdsToolkitConsole <Input> <Output> [--datapath [data]]");
            Console.WriteLine("Input can be a file or a directory, as long as the output is the other");
        }
        static void Main(string[] args)
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

            using (var file = new NdsRom())
            {
                if (!string.IsNullOrEmpty(dataOverride))
                {
                    file.DataPath = dataOverride;
                }

                file.OpenFile(filename, new PhysicalFileSystem()).Wait();
                
                if (File.Exists(filename))
                {
                    file.Unpack(dir, new PhysicalFileSystem()).Wait();
                }
                else if (Directory.Exists(filename))
                {
                    file.Save(dir, new PhysicalFileSystem()).Wait();
                }
            }                     
        }
    }
}