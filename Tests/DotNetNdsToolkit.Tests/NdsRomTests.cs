using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;
using SkyEditor.Core.IO;
using SkyEditor.Core.TestComponents;
using SkyEditor.Core.Utilities;
using System.Collections.Concurrent;
using System.Linq;
using SkyEditor.IO.FileSystem;
using SkyEditor.Utilities.AsyncFor;

namespace DotNetNdsToolkit.Tests
{
    [TestClass]
    public class NdsRomTests
    {
        public const string TestCategory = "NDS ROM";
        public const string EosUsPath = @"Resources/eosu.nds";
        public const string EosUsUnpackDir = @"RawFiles-EOSUS";
        public const string BrtUsPath = @"Resources/brtu.nds";
        public const string BrtUsUnpackDir = @"RawFiles-BRTUS";

        private IFileSystem SourceProvider { get; set; }
        private IFileSystem OutputProvider { get; set; }

        [TestInitialize]
        public void TestInit()
        {
            if (!File.Exists(EosUsPath))
            {
                Assert.Fail("Missing test ROM: Pokémon Mystery Dungeon: Explorers of Sky (US).  Place it at the following path: " + EosUsPath);
            }
            if (!File.Exists(BrtUsPath))
            {
                Assert.Fail("Missing test ROM: Pokémon Mystery Dungeon: Blue Rescue Team (US).  Place it at the following path: " + BrtUsPath);
            }
            SourceProvider = new PhysicalFileSystem();
            OutputProvider = new MemoryFileSystem();
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task EosUs_UnpackReportsProgress()
        {            
            using (var eosUS = new NdsRom())
            {
                // Arrange
                var progressReports = new ConcurrentBag<ProgressReportedEventArgs>();

                void onProgressed(object sender, ProgressReportedEventArgs e) {
                    progressReports.Add(e);
                }

                eosUS.ProgressChanged += onProgressed;

                await eosUS.OpenFile(EosUsPath, SourceProvider);

                // Act
                await eosUS.Unpack(EosUsUnpackDir, OutputProvider);

                // Assert
                // Make sure we have a reasonable distribution of percentages, and not all 0 or 1
                Assert.AreEqual(progressReports.Count, progressReports.Select(x => x.Progress).Count(), 0, "Too many duplicate progress percentages detected.");

                // Cleanup
                eosUS.ProgressChanged -= onProgressed;
            }

            // Cleanup
            OutputProvider.DeleteDirectory(EosUsUnpackDir);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task BrtUs_UnpackReportsProgress()
        {
            using (var brtUS = new NdsRom())
            {
                // Arrange
                var progressReports = new ConcurrentBag<ProgressReportedEventArgs>();

                void onProgressed(object sender, ProgressReportedEventArgs e)
                {
                    progressReports.Add(e);
                }

                brtUS.ProgressChanged += onProgressed;

                await brtUS.OpenFile(BrtUsPath, SourceProvider);

                // Act
                await brtUS.Unpack(BrtUsUnpackDir, OutputProvider);

                // Assert
                // Make sure we have a reasonable distribution of percentages, and not all 0 or 1
                Assert.AreEqual(progressReports.Count, progressReports.Select(x => x.Progress).Count(), 0, "Too many duplicate progress percentages detected.");

                // Cleanup
                brtUS.ProgressChanged -= onProgressed;
            }

            // Cleanup
            OutputProvider.DeleteDirectory(BrtUsUnpackDir);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestPackEOS()
        {
            using (var eosUS = new NdsRom())
            {
                await eosUS.OpenFile(EosUsPath, SourceProvider);
                await eosUS.Unpack(EosUsUnpackDir, OutputProvider);
                await eosUS.Save("eos-repack.nds", OutputProvider);

                using (var eosRepack = new NdsRom())
                {
                    await eosRepack.OpenFile("eos-repack.nds", OutputProvider);
                    await eosUS.Unpack(EosUsUnpackDir + "-Reunpack", OutputProvider);
                }
            }

            // Cleanup
            OutputProvider.DeleteFile("eos-repack.nds");
            OutputProvider.DeleteDirectory(EosUsUnpackDir + "-Reunpack");
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestPackBRT()
        {
            using (var eosUS = new NdsRom())
            {
                await eosUS.OpenFile(BrtUsPath, SourceProvider);
                await eosUS.Save("brt-repack.nds", OutputProvider);
            }

            // Cleanup
            OutputProvider.DeleteFile("eos-repack.nds");
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestAnalyzeEOS()
        {
            using (var eosUS = new NdsRom())
            {
                await eosUS.OpenFile(EosUsPath, SourceProvider);
                File.WriteAllText("analysis-eos.csv", eosUS.AnalyzeLayout().GenerateCSV());
            }
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestAnalyzeBRT()
        {
            using (var brtUS = new NdsRom())
            {
                await brtUS.OpenFile(BrtUsPath, SourceProvider);
                File.WriteAllText("analysis-brt.csv", brtUS.AnalyzeLayout(true).GenerateCSV());
            }
        }


    }
}
