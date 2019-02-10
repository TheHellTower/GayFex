using System.Reflection;

[assembly: AssemblyProduct("ConfuserEx 2")]
[assembly: AssemblyCompany("Martin Karing")]
[assembly: AssemblyCopyright("Copyright © 2018 - 2019 Martin Karing")]

#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
