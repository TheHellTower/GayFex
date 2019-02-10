using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type", Target = "~T:SevenZip.InvalidParamException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type", Target = "~T:SevenZip.DataErrorException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic", Scope = "type", Target = "~T:SevenZip.InvalidParamException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic", Scope = "type", Target = "~T:SevenZip.DataErrorException")]
[assembly: SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Scope = "type", Target = "~T:SevenZip.Compression.LZ.InWindow")]
[assembly: SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Scope = "type", Target = "~T:SevenZip.Compression.LZ.OutWindow")]

[assembly: SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", Scope = "type", Target = "~T:SevenZip.Compression.LZMA.Encoder")]

[assembly: SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Scope = "type", Target = "~T:SevenZip.Compression.LZMA.Encoder")]
[assembly: SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Scope = "type", Target = "~T:SevenZip.Compression.RangeCoder.Decoder")]

[assembly: SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Scope = "type", Target = "~T:SevenZip.CRC")]
[assembly: SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Scope = "type", Target = "~T:SevenZip.Compression.LZ.BinTree")]
[assembly: SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "type", Target = "~T:SevenZip.Compression.LZMA.Encoder")]
