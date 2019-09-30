---
layout: page
---
{% include setup %}

ConfuserEx 2 is an free, open-source protector for .NET applications.
It is the successor of [Confuser](http://confuser.codeplex.com) project
and the [ConfuserEx](https://github.com/yck1509/ConfuserEx) project.

---
<div class="row">
  <div class="col-md-6">
    <img class="img-responsive" alt="Screenshot of Command-line interface" src="{{ ASSET_PATH }}/screenshot1.png" style="height: 300px">
    <small>Command-line interface</small>
  </div>
  <div class="col-md-6">
    <img class="img-responsive" alt="Screenshot of Graphical interface" src="{{ ASSET_PATH }}/screenshot2.png" style="height: 300px">
    <small>Graphical interface</small>
  </div>
</div>
---

Features
--------
ConfuserEx supports .NET Framework from 2.0 - 4.7.2, .NET Standard, .NET Core and Mono.
It supports most of the protections you'll find in commerical protectors, and some more!

<div class="container-fluid">
  <p class="row">
    <ul class="col-md-4">
      <li>Symbol renaming</li>
      <li>WPF/BAML renaming</li>
      <li>Control flow obfuscation</li>
      <li>Method reference hiding</li>
    </ul>
    <ul class="col-md-4">
      <li>Anti debuggers/profilers</li>
      <li>Anti memory dumping</li>
      <li>Anti tampering (method encryption)</li>
      <li>Embedding dependency</li>
    </ul>
    <ul class="col-md-4">
      <li>Constant encryption</li>
      <li>Resource encryption</li>
      <li>Compressing output</li>
      <li>Extensible plugin API</li>
    </ul>
  </p>
</div>

---
<div class="row">
  <div class="col-md-6">
    <img class="img-responsive" alt="Assembly loaded in ILSpy before protection" src="{{ ASSET_PATH }}/prot1.png">
    <small>Before protection</small>
  </div>
  <!--
      Umm... Actually I think it's a bit unfair to use invalid metadata protection in this image,
      but I can assure you that, even if you don't use invalid metadata, the protection is still
      very good! :)
  -->
  <div class="col-md-6">
    <img class="img-responsive" alt="Assembly loaded in ILSpy after protection" src="{{ ASSET_PATH }}/prot2.png">
    <small>After protection</small>
  </div>
</div>
---

Downloads
---------
You could obtain the latest source code and releases at [GitHub project page](https://github.com/mkaring/ConfuserEx/releases).
You may find the bleeding edge builds at [the CI Server](https://ci.appveyor.com/project/mkaring/confuserex).
ConfuserEx requires .NET Framework 4.6.1 to run. The CLI interface runs also on .NET Core 2.2 on multiple platforms.
It might be helpful to read the [FAQ]({{ BASE_PATH }}/faq/)!

---

Contribution
------------
ConfuserEx is licensed under [MIT license](http://opensource.org/licenses/MIT), 
so you're free to fork and modify it to suit your need!
You could also contribute to the project by creating pull requests and [reporting bugs]({{ BASE_PATH }}/issues/)!

---

Donation
---------
If you find ConfuserEx 2 helpful and want to donate to support my work on the project you can support me on
[open collective](https://opencollective.com/confuserex), [liberapay](https://liberapay.com/mkaring/) or 
you can [buy me a coffee](http://buymeacoff.ee/fFUnXMCdW) :coffee:.