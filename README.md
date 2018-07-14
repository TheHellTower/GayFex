# ConfuserEx

[![Build status](https://ci.appveyor.com/api/projects/status/so65dx6p7gq3f14l/branch/master?svg=true)](https://ci.appveyor.com/project/mkaring/confuserex/branch/master)

ConfuserEx is a open-source protector for .NET applications.
It is the successor of [Confuser](http://confuser.codeplex.com) project.

## Features

* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5/4.6/4.7
* Symbol renaming (Support WPF/BAML)
* Protection against debuggers/profilers
* Protection against memory dumping
* Protection against tampering (method encryption)
* Control flow obfuscation
* Constant/resources encryption
* Reference hiding proxies
* Disable decompilers
* Embedding dependency
* Compressing output
* Extensible plugin API
* Many more are coming!

# Usage

```Batchfile
Confuser.CLI.exe <path to project file>
```

The project file is a ConfuserEx Project (*.crproj).
The format of project file can be found in docs\ProjectFormat.md

# Bug Report
See the [Issues Report](https://github.com/mkaring/ConfuserEx/issues) section of website.


# License

Licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.

# Credits

**[0xd4d](https://github.com/0xd4d)** for his awesome work and extensive knowledge!  
Members of **[Black Storm Forum](http://board.b-at-s.info/)** for their help!
