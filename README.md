# ConfuserEx 2

[![Build status][img_build]][build]
[![Test status][img_test]][test]
[![CodeFactor][img_codefactor]][codefactor]
[![Gitter Chat][img_gitter]][gitter]
[![MIT License][img_license]][license]

ConfuserEx 2 is a open-source protector for .NET applications.
It is the successor of [Confuser][confuser] project and the [ConfuserEx][yck1509_confuserex] project.

The development is currently in alpha stage. While the features of the original ConfuserEx are implementedand working,
the features that are part of the new version 2.0 are still in development and may not yield the desired results.

## Features

* Supported runtimes:
  * .NET Framework 2.0 - 4.7.2
  * .NET Standard  1.0 - 2.0
  * .NET Core      1.0 - 2.2
* Protections
  * Symbol renaming (Support WPF/BAML)
  * Protection against debuggers/profilers
  * Protection against memory dumping
  * Protection against tampering (method encryption)
  * Control flow obfuscation
  * Constant/resources encryption
  * Reference hiding proxies
  * Disable decompilers
* Optimizations
  * Compiling regular expressions
  * Optimizing tail calls and tail recursions
* Deployment
  * Embedding dependency
  * Compressing output
* Extensible plugin API based on the [Managed Extensibility Framework (MEF)][mef]
* MSBuild Integration

# Usage

## Command Line

```Batchfile
Confuser.CLI.exe <path to project file>
```

The project file is a ConfuserEx Project (`*.crproj`).
The format of project file can be found in [docs\ProjectFormat.md][project_format]

## User Interface

The `ConfuserEx.exe` provides a WPF based user interface for the Windows Platform. Simply start the executable, the
user interface allows setting up the confuser project files.

## MSBuild

ConfuserEx 2 has a integration into MSBuild using the NuGet Package that is produced by the `Confuser.MSBuild.Tasks`
project. Once enabled in any other .NET project it will find a `*.crproj` file next to the project file and populate
it automatically with the probing paths of all the dependency assemblies (including the NuGet packages) of the project
and create the obfuscated assemblies automatically.

The nuget package can be accessed using the [AppVeyor Nuget Feed][build_nuget]
or it has to be self-hosted.

# Bug Report

See the [Issues Report][issues] section of website.

# Supporting ConfuserEx 2

I gladly accept pull-request for bugs and new additions to ConfuserEx. If you noticed any problem or have and idea how
to improve ConfuserEx 2, do not hesitate to add those ideas as feature requests to the 
[Issues][issues] section.

# License

Licensed under the MIT license. See [LICENSE.md][license] for details.

# Donation

If you find ConfuserEx 2 helpful and want to donate to support my work on the project you can support me on
[GitHub Sponsors][sponsors] or you can [buy me a coffee] :coffee:.

# Credits

**[Ki (yck1509)][yck1509]** for the original ConfuserEx.  
**[0xd4d]** for his awesome work and extensive knowledge!  

Thank you to [JetBrains] for providing the open source licenses for their products I'm
using to develop ConfuserEx 2. Most notably: 

[<img src="Assets/resharper.png" height="50" />][resharper]

[0xd4d]: https://github.com/0xd4d
[build]: https://ci.appveyor.com/project/mkaring/confuserex/branch/master
[build_nuget]: https://ci.appveyor.com/nuget/confuserex-r4olq7m3uysu
[buy me a coffee]: http://buymeacoff.ee/fFUnXMCdW
[codefactor]: https://www.codefactor.io/repository/github/mkaring/confuserex/overview/release/2.0
[confuser]: http://confuser.codeplex.com
[gitter]: https://gitter.im/ConfuserEx/community
[issues]: https://github.com/mkaring/ConfuserEx/issue
[JetBrains]: https://www.jetbrains.com/
[license]: LICENSE.md
[mef]: https://docs.microsoft.com/dotnet/framework/mef/ "Managed Extensibility Framework (MEF) | Microsoft Docs"
[project_format]: docs/ProjectFormat.md
[resharper]: https://www.jetbrains.com/resharper/
[sponsors]: https://github.com/sponsors/mkaring
[test]: https://ci.appveyor.com/project/mkaring/confuserex/branch/release/2.0/tests
[yck1509]: https://github.com/yck1509
[yck1509_confuserex]: https://yck1509.github.io/ConfuserEx/

[img_build]: https://img.shields.io/appveyor/ci/mkaring/ConfuserEx/release/2.0.svg?style=flat
[img_codefactor]: https://www.codefactor.io/repository/github/mkaring/confuserex/badge/release/2.0
[img_gitter]: https://img.shields.io/gitter/room/mkaring/ConfuserEx.svg?style=flat
[img_license]: https://img.shields.io/github/license/mkaring/ConfuserEx.svg?style=flat
[img_test]: https://img.shields.io/appveyor/tests/mkaring/ConfuserEx/release/2.0.svg?style=flat&compact_message