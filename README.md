# ConfuserEx

[![Build status][img_build]][build]
[![Test status][img_test]][test]
[![CodeFactor][img_codefactor]][codefactor]
[![Gitter Chat][img_gitter]][gitter]
[![MIT License][img_license]][license]

<details>
    <summary>
        <h3>Original ReadMe</h3>
    </summary>

ConfuserEx is a open-source protector for .NET applications.
It is the successor of [Confuser][confuser] project.

## Features

* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5/4.6/4.7/4.8
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

The project file is a ConfuserEx Project (`*.crproj`).
The format of project file can be found in [docs\ProjectFormat.md][project_format]

# Bug Report

See the [Issues Report][issues] section of website.

# License

Licensed under the MIT license. See [LICENSE.md][license] for details.

# Credits

**[0xd4d]** for his awesome work and extensive knowledge !
</br>
**[Martin Karing]** for his awesome updates on ConfuserEx !
</details>

> **warning** This ConfuserEx repository isn't meant to be taken seriously, if you have any problem, you are advised to try and solve them yourself before opening an issue.


[0xd4d]: https://github.com/0xd4d
[Martin Karing]: https://github.com/mkaring
[build]: https://ci.appveyor.com/project/thehelltower/gayfex/branch/master
[codefactor]: https://www.codefactor.io/repository/github/thehelltower/gayfex/overview/master
[confuser]: http://confuser.codeplex.com
[issues]: https://github.com/thehelltower/gayfex/issues
[gitter]: https://gitter.im/ConfuserEx/community
[license]: LICENSE.md
[project_format]: docs/ProjectFormat.md
[test]: https://ci.appveyor.com/project/thehelltower/gayfex/branch/master/tests

[img_build]: https://img.shields.io/appveyor/ci/thehelltower/gayfex/master.svg?style=flat
[img_codefactor]: https://www.codefactor.io/repository/github/thehelltower/gayfex/badge/master
[img_gitter]: https://img.shields.io/gitter/room/thehelltower/gayfex.svg?style=flat
[img_license]: https://img.shields.io/github/license/thehelltower/gayfex.svg?style=flat
[img_test]: https://img.shields.io/appveyor/tests/thehelltower/gayfex/master.svg?style=flat&compact_message
