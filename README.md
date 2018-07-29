# ConfuserEx 2

[![Build status](https://ci.appveyor.com/api/projects/status/so65dx6p7gq3f14l/branch/release/2.0?svg=true)](https://ci.appveyor.com/project/mkaring/confuserex/branch/release/2.0)

ConfuserEx is a open-source protector for .NET applications.
It is the successor of [Confuser](http://confuser.codeplex.com) project and the [ConfuserEx](https://yck1509.github.io/ConfuserEx/) project.

The development is currently in alpha stage. While the features of the original ConfuserEx are implementedand working,
the features that are part of the new version 2.0 are still in development and may be yield the desired results.

## Features

* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5/4.6/4.7
* Symbol renaming (Support WPF/BAML)
* Protection against debuggers/profilers
* Protection against memory dumping
* Protection against tampering (method encryption)
* Control flow obfuscation
* _Type Scrambling using Generics (current not working in case generic classes are present in the protected assembly.)_
* Constant/resources encryption
* Reference hiding proxies
* Disable decompilers
* Embedding dependency
* Compressing output
* Extensible plugin API based on the [Managed Extensibility Framework (MEF)](https://docs.microsoft.com/dotnet/framework/mef/ "Managed Extensibility Framework (MEF) | Microsoft Docs")
* MSBuild Integration

# Usage

## Command Line

```Batchfile
Confuser.CLI.exe <path to project file>
```

The project file is a ConfuserEx Project (*.crproj).
The format of project file can be found in docs\ProjectFormat.md

## User Interface

The `ConfuserEx.exe` provides a WPF based user interface for the Windows Platform. Simply start the executable, the
user interface allows setting up the confuser project files.

## MSBuild

ConfuserEx 2 has a integration into MSBuild using the NuGet Package that is produced by the `Confuser.MSBuild.Tasks`
project. Once enabled in any other .NET project it will find a `*.crproj` file next to the project file and populate
it automatically with the probing paths of all the dependency assemblies (including the NuGet packages) of the project
and create the obfuscated assemblies automatically.

The nuget package can be accessed using the [AppVeyor Nuget Feed](https://ci.appveyor.com/nuget/confuserex-r4olq7m3uysu)
or it has to be self-hosted.


# Bug Report
See the [Issues Report](https://github.com/mkaring/ConfuserEx/issues) section of website.


# License

Licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.

# Credits

**[Ki (yck1509)](https://github.com/yck1509)** for the original ConfuserEx.  
**[0xd4d](https://github.com/0xd4d)** for his awesome work and extensive knowledge!  
Members of **[Black Storm Forum](http://board.b-at-s.info/)** for their help!
