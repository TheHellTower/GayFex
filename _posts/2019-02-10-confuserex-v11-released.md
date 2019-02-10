---
layout: post
title: ConfuserEx v1.1.0 Released!
---
{% include setup %}

ConfuserEx is still alive and kicking. The updated version 1.1 contains mostly bugfixes. The big thing is the update of [`dnlib`](https://github.com/0xd4d/dnlib) to version 3. This allows ConfuserEx to work with the new portable debug symbol files and with .NET Core assemblies.

There are also some bugfixes, highlights being the compressor being able to handle satellite assemblies propertly (at least with .NET Framework >= 4.0) and the XAML renaming handling more.

You can review all changes done here: [Changes in v1.1.0](https://github.com/mkaring/ConfuserEx/compare/v1.0.0...v1.1.0)

<div class="well well-lg">
  <div class="row">
    <div class="col-md-6 text-center">
      <a class="btn btn-primary btn-lg" role="button" href="https://github.com/mkaring/ConfuserEx/releases/download/v1.1.0/ConfuserEx.zip">Download binaries</a>
    </div>
    <div class="col-md-6 text-center">
      <a class="btn btn-primary btn-lg" role="button" href="https://github.com/mkaring/ConfuserEx/archive/v1.1.0.zip">Download source code</a>
    </div>
  </div>
</div>

You can also get the bleeding edge builds from [the CI Server](https://ci.appveyor.com/project/mkaring/confuserex)!!!