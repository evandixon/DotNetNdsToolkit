using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using SkyEditor.IO.FileSystem;
using SkyEditor.Utilities.AsyncFor;
using SkyEditor.IO.Binary;

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
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task EosUs_UnpackReportsProgress()
        {
            // Arrange
            var outputProvider = new InMemoryFileSystem();
            using var eosUS = await NdsRom.LoadFromFile(EosUsPath);
            var progressReportToken = new ProgressReportToken();
            var progressReports = new ConcurrentBag<ProgressReportedEventArgs>();

            void onProgressed(object? sender, ProgressReportedEventArgs e)
            {
                progressReports.Add(e);
            }

            progressReportToken.ProgressChanged += onProgressed;

            // Act
            await eosUS.Unpack(EosUsUnpackDir, outputProvider, progressReportToken);

            // Assert
            // Make sure we have a reasonable distribution of percentages, and not all 0 or 1
            Assert.AreEqual(progressReports.Count, progressReports.Select(x => x.Progress).Count(), 0, "Too many duplicate progress percentages detected.");

            // Cleanup
            progressReportToken.ProgressChanged -= onProgressed;
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task BrtUs_UnpackReportsProgress()
        {
            // Arrange
            var sourceProvider = PhysicalFileSystem.Instance;
            var outputProvider = new InMemoryFileSystem();
            using var brtUS = await NdsRom.LoadFromFile(BrtUsPath);
            var progressReportToken = new ProgressReportToken();
            var progressReports = new ConcurrentBag<ProgressReportedEventArgs>();

            void onProgressed(object? sender, ProgressReportedEventArgs e)
            {
                progressReports.Add(e);
            }

            progressReportToken.ProgressChanged += onProgressed;

            // Act
            await brtUS.Unpack(BrtUsUnpackDir, outputProvider, progressReportToken);

            // Assert
            // Make sure we have a reasonable distribution of percentages, and not all 0 or 1
            Assert.AreEqual(progressReports.Count, progressReports.Select(x => x.Progress).Count(), 0, "Too many duplicate progress percentages detected.");

            // Cleanup
            progressReportToken.ProgressChanged -= onProgressed;
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestPackEOS()
        {
            var outputProvider = new InMemoryFileSystem();
            using var eosUS = await NdsRom.LoadFromFile(EosUsPath);
            await eosUS.Unpack(EosUsUnpackDir, outputProvider);
            await eosUS.Save("eos-repack.nds", outputProvider);

            using var eosRepackData = new BinaryFile("eos-repack.nds", outputProvider);
            using var eosRepack = await NdsRom.LoadFromFile(eosRepackData);
            await eosUS.Unpack(EosUsUnpackDir + "-Reunpack", outputProvider);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestPackBRT()
        {
            var outputProvider = new InMemoryFileSystem();
            using var brtRomData = new BinaryFile(BrtUsPath);
            using var eosUS = await NdsRom.LoadFromFile(brtRomData);
            await eosUS.Save("brt-repack.nds", outputProvider);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestAnalyzeEOS()
        {
            using var eosUS = await NdsRom.LoadFromFile(EosUsPath);
            File.WriteAllText("analysis-eos.csv", eosUS.AnalyzeLayout().GenerateCSV());
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public async Task TestAnalyzeBRT()
        {
            using var brtUS = await NdsRom.LoadFromFile(BrtUsPath);
            File.WriteAllText("analysis-brt.csv", brtUS.AnalyzeLayout(true).GenerateCSV());
        }


    }
}
