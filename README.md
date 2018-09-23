# DotNetNdsToolkit
.Net Core (aka cross-platform) replacement for DarkFader's ndstool

## Building

Building requires the .Net Core 2.0

1. dotnet restore DotNetNdsToolkit.sln

The following package sources must be referenced:

```
https://api.nuget.org/v3/index.json
https://www.myget.org/F/skyeditor/api/v3/index.json
```

2. dotnet build DotNetNdsToolkit.sln

## Usage
### DotNetNdsToolkit (Code Library)
Usage instructions coming soon.
### NdsToolkitConsole (Console Application)
```
NdsToolkitConsole <Input> <Output> [--datapath [data]]
```
Input can be a file or a directory, as long as the output is the other");