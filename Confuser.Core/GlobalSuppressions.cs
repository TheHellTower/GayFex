
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type", Target = "~T:SevenZip.InvalidParamException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type", Target = "~T:SevenZip.DataErrorException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic", Scope = "type", Target = "~T:SevenZip.InvalidParamException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic", Scope = "type", Target = "~T:SevenZip.DataErrorException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Scope = "type", Target = "~T:SevenZip.Compression.LZ.InWindow")]
[assembly: SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Scope = "type", Target = "~T:SevenZip.Compression.LZ.OutWindow")]

[assembly: SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Scope = "type", Target = "~T:SevenZip.Compression.LZMA.Encoder")]
[assembly: SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Scope = "type", Target = "~T:SevenZip.Compression.RangeCoder.Decoder")]

