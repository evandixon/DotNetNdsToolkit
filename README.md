# SkyEditor.NdsToolkit
.Net Core (aka cross-platform) replacement for DarkFader's ndstool


## Usage

### NdsToolkitConsole (Console Application)

```
NdsToolkitConsole <Input> <Output> [--datapath [data]]
```

Input can be a file or a directory, as long as the output is the other.

### SkyEditor.NdsToolkit (Code Library)

Load from a file:

```
using var ndsRom = await NdsRom.LoadFromFile("filename.nds");
```

Or a directory:

```
using var ndsRom = await NdsRom.LoadFromDirectory("unpacked");
```

Or other sources using [SkyEditor.IO](https://github.com/evandixon/SkyEditor.IO):

```
using var zipArchive = ZipFile.OpenAsync("archive.zip", ZipArchiveMode.Read);
using var zip = new ZipFileSystem(zipArchive);
using var file = await NdsRom.LoadFromFile("/filename.zip", zip);
```

Once opened...

Extract to a directory:

```
await ndsRom.Unpack("unpacked");
```

Update files inside the ROM without even having to extract first.

Use [SkyEditor.IO](https://github.com/evandixon/SkyEditor.IO) to browse files with an interface that feels like System.IO.

Updates are stored in memory until saved.

```
using System.IO;

var files = ndsRom.FileSystem.GetFiles("/", "*", topDirectoryOnly: false);
ndsRom.FileSystem.ReadAllBytes("/arm9.bin");
ndsRom.FileSystem.ReadAllBytes("/overlay/overlay_0013.bin");
ndsRom.FileSystem.ReadAllBytes("/data/MESSAGE/text_e.str");
ndsRom.FileSystem.WriteAllBytes("/arm9.bin", yourByteArrayHere);
```

Save your changes to a new ROM:
```
await ndsRom.Save("file-modified.nds");

```