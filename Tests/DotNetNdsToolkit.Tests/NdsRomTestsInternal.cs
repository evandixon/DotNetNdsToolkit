using DotNetNdsToolkit.Subtypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyEditor.IO.FileSystem;

namespace DotNetNdsToolkit.Tests
{
    [TestClass]
    public class NdsRomTestsInternal
    {
        public const string TestCategory = "NDS ROM (Internal)";

        public class TestNdsRomFileSystem : NdsFileSystem
        {
            public TestNdsRomFileSystem() 
                : base(NdsRom.LoadFromDirectory("/", new InMemoryFileSystem()), "/", new InMemoryFileSystem(), false)
            {
            }

            public new string[] GetPathParts(string path)
            {
                return base.GetPathParts(path);
            }
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public void GetPathParts_Root()
        {
            var testRom = new TestNdsRomFileSystem();
            var parts = testRom.GetPathParts("/");
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual("", parts[0]);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public void GetPathParts_OverlayX_Absolute()
        {
            var testRom = new TestNdsRomFileSystem();
            var parts = testRom.GetPathParts("/overlay/overlay_0000.bin");
            Assert.AreEqual(2, parts.Length);
            Assert.AreEqual("overlay", parts[0]);
            Assert.AreEqual("overlay_0000.bin", parts[1]);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public void GetPathParts_OverlayX_RelativeToRoot()
        {
            var testRom = new TestNdsRomFileSystem();
            var parts = testRom.GetPathParts("overlay/overlay_0000.bin");
            Assert.AreEqual(2, parts.Length);
            Assert.AreEqual("overlay", parts[0]);
            Assert.AreEqual("overlay_0000.bin", parts[1]);
        }

        [TestMethod]
        [TestCategory(TestCategory)]
        public void GetPathParts_OverlayX_RelativeToOverlay()
        {
            var testRom = new TestNdsRomFileSystem();
            (testRom as IFileSystem).WorkingDirectory = "/overlay";
            var parts = testRom.GetPathParts("overlay_0000.bin");
            Assert.AreEqual(2, parts.Length);
            Assert.AreEqual("overlay", parts[0]);
            Assert.AreEqual("overlay_0000.bin", parts[1]);
        }
    }
}
