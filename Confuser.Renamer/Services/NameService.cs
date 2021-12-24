using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Properties;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer.Services {
	internal class NameService : INameService {
		static readonly object CanRenameKey = new object();
		static readonly object RenameModeKey = new object();
		static readonly object ReferencesKey = new object();
		static readonly object DisplayNameKey = new object();
		static readonly object NormalizedNameKey = new object();
		static readonly object IsRenamedKey = new object();

		private readonly ReadOnlyMemory<byte> nameSeed;
		readonly IRandomGenerator random;
		readonly VTableStorage storage;
		private readonly NameProtection _parent;

		readonly HashSet<string> identifiers = new HashSet<string>();

		long _nameId;
		readonly StringBuilder _nameBuilder = new StringBuilder();
		readonly Dictionary<string, string> _originalToObfuscatedNameMap = new Dictionary<string, string>();
		readonly Dictionary<string, string> _obfuscatedToOriginalNameMap = new Dictionary<string, string>();
		readonly Dictionary<string, string> _prefixesMap = new Dictionary<string, string>();
		internal ReversibleRenamer reversibleRenamer;

		internal NameService(IServiceProvider provider, NameProtection parent) {
			_parent = parent ?? throw new ArgumentNullException(nameof(parent));
			storage = new VTableStorage(provider);
			random = provider.GetRequiredService<IRandomService>().GetRandomGenerator(NameProtection._FullId);
			nameSeed = random.NextBytes(20);

			Renamers = ImmutableArray.Create<IRenamer>(
				new InterReferenceAnalyzer(),
				new VTableAnalyzer(),
				new TypeBlobAnalyzer(),
				new ResourceAnalyzer(),
				new LdtokenEnumAnalyzer(),
				new ManifestResourceAnalyzer(),
				new ReflectionAnalyzer(),
				new CallSiteAnalyzer()
			);
		}

		public IImmutableList<IRenamer> Renamers { get; private set; }

		public VTableStorage GetVTables() {
			return storage;
		}

		public bool CanRename(IConfuserContext context, IDnlibDef def) {
			if (context == null) throw new ArgumentNullException(nameof(context));

			if (def == null || !context.GetParameters(def).HasParameters(_parent))
				return false;

			return context.Annotations.Get(def, CanRenameKey, true);
		}

		public void SetCanRename(IConfuserContext context, IDnlibDef def, bool val) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (def == null) throw new ArgumentNullException(nameof(def));

			context.Annotations.Set(def, CanRenameKey, val);
		}

		public void SetParam<T>(IConfuserContext context, IDnlibDef def, IProtectionParameter<T> protectionParameter, T value) {
			string serializedValue = protectionParameter.Serialize(value);
			context.GetParameters(def).SetParameter(_parent, protectionParameter.Name, serializedValue);
		}

		public T GetParam<T>(IConfuserContext context, IDnlibDef def, IProtectionParameter<T> protectionParameter) {
			var parameters = context.GetParameters(def);
			if (!parameters.HasParameter(_parent, protectionParameter.Name)) return protectionParameter.DefaultValue;
			
			string value = context.GetParameters(def).GetParameter(_parent, protectionParameter.Name);
			return protectionParameter.Deserialize(value);
		}

		public RenameMode GetRenameMode(IConfuserContext context, object obj) {
			return context.Annotations.Get(obj, RenameModeKey, RenameMode.Unicode);
		}

		public void SetRenameMode(IConfuserContext context, object obj, RenameMode val) {
			context.Annotations.Set(obj, RenameModeKey, val);
		}

		public void ReduceRenameMode(IConfuserContext context, object obj, RenameMode val) {
			RenameMode original = GetRenameMode(context, obj);
			if (original < val)
				context.Annotations.Set(obj, RenameModeKey, val);
			if (val <= RenameMode.Reflection && obj is IDnlibDef dnlibDef) {
				var name = ExtractDisplayNormalizedName(context, dnlibDef, true);
				SetNormalizedName(context, dnlibDef, name.NormalizedName);
			}
		}

		public void AddReference<T>(IConfuserContext context, T obj, INameReference<T> reference) {
			context.Annotations.GetOrCreate(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
		}

		public void Analyze(IConfuserContext context, IDnlibDef def) {
			var analyze = context.Pipeline.FindPhase<AnalyzePhase>();

			StoreNames(context, def);
			if (def is TypeDef typeDef) {
				GetVTables().GetVTable(typeDef);
			}

			analyze.Analyze(this, context, EmptyProtectionParameters.Instance, def, true);
		}

		public void SetNameId(uint id) {
			_nameId = id;
		}


		string ObfuscateNameInternal(ReadOnlySpan<byte> hash, RenameMode mode) {
			switch (mode) {
				case RenameMode.Empty:
					return "";
				case RenameMode.Unicode:
					return Utils.EncodeString(hash, unicodeCharset) + "\u202e";
				case RenameMode.Letters:
					return Utils.EncodeString(hash, letterCharset);
				case RenameMode.ASCII:
					return Utils.EncodeString(hash, asciiCharset);
				case RenameMode.Reflection:
					return Utils.EncodeString(hash, reflectionCharset);
				case RenameMode.Decodable:
					return "_" + Utils.EncodeString(hash, alphaNumCharset);
				case RenameMode.Sequential:
					return "_" + GetNextSequentialName();
				default:
					throw new NotSupportedException("Rename mode '" + mode + "' is not supported.");
			}
		}

		string ParseGenericName(string name, out int count) {
			int graveIndex = name.LastIndexOf('`');
			if (graveIndex != -1) {
				if (int.TryParse(name.Substring(graveIndex + 1), out int c)) {
					count = c;
					return name.Substring(0, graveIndex);
				}
			}

			count = 0;
			return name;
		}

		string MakeGenericName(string name, int count) => count == 0 ? name : $"{name}`{count}";

		public string ObfuscateName(string name, RenameMode mode) => ObfuscateName(null, name, mode, false);

		public string ObfuscateName(IConfuserContext context, IDnlibDef dnlibDef, RenameMode mode) {
			var normalizedName = GetNormalizedName(context, dnlibDef);
			bool preserveGenericParams = GetParam(context, dnlibDef, _parent.Parameters.PreserveGenericParams);
			return ObfuscateName(null, normalizedName, mode, preserveGenericParams);
		}

		public string ObfuscateName(string format, string name, RenameMode mode, bool preserveGenericParams = false) {
			int genericParamsCount = 0;
			if (preserveGenericParams) {
				name = ParseGenericName(name, out genericParamsCount);
			}

			string newName;

			if (string.IsNullOrEmpty(name) || mode == RenameMode.Empty)
				return string.Empty;

			if (mode == RenameMode.Debug || mode == RenameMode.Retain) {
				// When flattening there are issues, in case there is a . in the name of the assembly.
				newName = name.Replace('.', '_');
				newName = mode == RenameMode.Debug ? "_" + newName : newName;
			}
			else if (mode == RenameMode.Reversible) {
				if (reversibleRenamer == null)
					throw new ArgumentException("Password not provided for reversible renaming.");
				newName = reversibleRenamer.Encrypt(name);
			}
			else if (!_originalToObfuscatedNameMap.TryGetValue(name, out newName)) {
				byte[] hash = Utils.Xor(Utils.SHA1(Encoding.UTF8.GetBytes(name)), nameSeed.Span);
				while (true) {
					newName = ObfuscateNameInternal(hash, mode);

					try {
						if (!(format is null))
							newName = string.Format(CultureInfo.InvariantCulture, format, newName);
					}
					catch (FormatException ex) {
						throw new ArgumentException(
							string.Format(CultureInfo.InvariantCulture,
								Resources.NameService_ObfuscateName_InvalidFormat, format),
							nameof(format), ex);
					}

					if (!identifiers.Contains(MakeGenericName(newName, genericParamsCount))
					    && !_obfuscatedToOriginalNameMap.ContainsKey(newName))
						break;
					hash = Utils.SHA1(hash);
				}

				if (mode == RenameMode.Decodable || mode == RenameMode.Sequential) {
					_obfuscatedToOriginalNameMap.Add(newName, name);
					_originalToObfuscatedNameMap.Add(name, newName);
				}
			}

			return MakeGenericName(newName, genericParamsCount);
		}

		public string RandomName() {
			return RandomName(RenameMode.Unicode);
		}

		public string RandomName(RenameMode mode) {
			Span<byte> buf = stackalloc byte[16];
			random.NextBytes(buf);
			return ObfuscateName(Utils.ToHexString(buf), mode);
		}

		public void StoreNames(IConfuserContext context, IDnlibDef dnlibDef) {
			AddReservedIdentifier(dnlibDef.Name);
			if (dnlibDef is TypeDef typeDef) {
				AddReservedIdentifier(typeDef.Namespace);
			}

			var name = ExtractDisplayNormalizedName(context, dnlibDef);
			context.Annotations.Set(dnlibDef, DisplayNameKey, name.DisplayName);
			context.Annotations.Set(dnlibDef, NormalizedNameKey, name.NormalizedName);
		}

		public void SetNormalizedName(IConfuserContext context, IDnlibDef dnlibDef, string name) {
			context.Annotations.Set(dnlibDef, NormalizedNameKey, name);
		}

		public void AddReservedIdentifier(string id) => identifiers.Add(id);

		public void RegisterRenamer(IRenamer renamer) {
			Renamers = Renamers.Add(renamer);
		}

		public T FindRenamer<T>() {
			return Renamers.OfType<T>().Single();
		}

		public void MarkHelper(IConfuserContext context, IDnlibDef def, IMarkerService marker,
			IConfuserComponent parentComp) {
			if (marker.IsMarked(context, def))
				return;
			// TODO: Private definitions are not properly handled there. They get a wider visibility.
			if (def is MethodDef method) {
				method.Access = MethodAttributes.Assembly;
				if (!method.IsSpecialName && !method.IsRuntimeSpecialName && !method.DeclaringType.IsDelegate())
					method.Name = RandomName();
			}
			else if (def is FieldDef field) {
				field.Access = FieldAttributes.Assembly;
				if (!field.IsSpecialName && !field.IsRuntimeSpecialName)
					field.Name = RandomName();
			}
			else if (def is TypeDef type) {
				type.Visibility = type.DeclaringType == null ? TypeAttributes.NotPublic : TypeAttributes.NestedAssembly;
				type.Namespace = "";
				if (!type.IsSpecialName && !type.IsRuntimeSpecialName)
					type.Name = RandomName();
			}

			SetCanRename(context, def, false);
			Analyze(context, def);
			marker.Mark(context, def, parentComp);
		}

		#region Charsets

		static readonly char[] asciiCharset = Enumerable.Range(32, 95)
			.Select(ord => (char)ord)
			.Except(new[] {'.'})
			.ToArray();

		static readonly char[] reflectionCharset = asciiCharset.Except(new[] {' ', '[', ']'}).ToArray();

		static readonly char[] letterCharset = Enumerable.Range(0, 26)
			.SelectMany(ord => new[] {(char)('a' + ord), (char)('A' + ord)})
			.ToArray();

		static readonly char[] alphaNumCharset = Enumerable.Range(0, 26)
			.SelectMany(ord => new[] {(char)('a' + ord), (char)('A' + ord)})
			.Concat(Enumerable.Range(0, 10).Select(ord => (char)('0' + ord)))
			.ToArray();

		// Especially chosen, just to mess with people.
		// Inspired by: http://xkcd.com/1137/ :D
		static readonly char[] unicodeCharset = new char[] { }
			.Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x2029, 6).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
			.Except(new[] {'\u2029'})
			.ToArray();

		#endregion

		public IRandomGenerator GetRandom() {
			return random;
		}

		public IList<INameReference> GetReferences(IConfuserContext context, object obj) {
			return context.Annotations.GetLazy(obj, ReferencesKey, key => new List<INameReference>());
		}

		public string GetDisplayName(IConfuserContext context, IDnlibDef obj) =>
			context.Annotations.Get(obj, DisplayNameKey, (string)null);

		public string GetNormalizedName(IConfuserContext context, IDnlibDef obj) =>
			context.Annotations.Get(obj, NormalizedNameKey, (string)null);

		public IReadOnlyDictionary<string, string> GetNameMap() => _obfuscatedToOriginalNameMap;

		public bool IsRenamed(IConfuserContext context, IDnlibDef def) => context.Annotations.Get(def, IsRenamedKey, !CanRename(context, def));

		public void SetIsRenamed(IConfuserContext context, IDnlibDef def) => context.Annotations.Set(def, IsRenamedKey, true);

		public DisplayNormalizedName ExtractDisplayNormalizedName(IConfuserContext context, IDnlibDef dnlibDef, bool forceShortNames = false) {
			var shortNames = forceShortNames || GetParam(context, dnlibDef, _parent.Parameters.ShortNames);
			var renameMode = GetRenameMode(context, dnlibDef);

			if (dnlibDef is TypeDef typeDef) {
				if (typeDef.DeclaringType != null) {
					var outerClassName = CompressTypeName(typeDef.DeclaringType.FullName, renameMode);
					return
						new DisplayNormalizedName(dnlibDef.FullName, $"{outerClassName.NormalizedName}/{dnlibDef.Name}");
				}

				return new DisplayNormalizedName(dnlibDef.FullName, dnlibDef.FullName);
			}

			var displayNameBuilder = new StringBuilder();
			var normalizedNameBuilder = new StringBuilder();
			if (dnlibDef is IMemberDef memberDef) {
				var declaringTypeName = CompressTypeName(memberDef.DeclaringType?.FullName ?? "", renameMode);

				displayNameBuilder.Append(declaringTypeName.DisplayName);
				displayNameBuilder.Append("::");
				displayNameBuilder.Append(dnlibDef.Name);

				normalizedNameBuilder.Append(declaringTypeName.NormalizedName);
				normalizedNameBuilder.Append("::");
				normalizedNameBuilder.Append(dnlibDef.Name);

				if (memberDef is MethodDef methodDef) {
					displayNameBuilder.Append('(');
					normalizedNameBuilder.Append('(');
					if (methodDef.Signature is MethodSig methodSig) {
						var methodParams = methodSig.Params;
						for (var index = 0; index < methodParams.Count; index++) {
							var parameterName = CompressTypeName(methodParams[index].ToString(), renameMode);
							displayNameBuilder.Append(parameterName.DisplayName);
							normalizedNameBuilder.Append(parameterName.NormalizedName);

							if (index < methodParams.Count - 1) {
								displayNameBuilder.Append(',');
								normalizedNameBuilder.Append(',');
							}
						}
					}

					displayNameBuilder.Append(')');
					normalizedNameBuilder.Append(')');
				}
			}

			return new DisplayNormalizedName(displayNameBuilder.ToString(),
				shortNames ? dnlibDef.Name.ToString() : normalizedNameBuilder.ToString());
		}

		DisplayNormalizedName CompressTypeName(string typeName, RenameMode renameMode)
		{
			if (renameMode == RenameMode.Reversible)
			{
				if (!_prefixesMap.TryGetValue(typeName, out string prefix))
				{
					_prefixesMap.Add(typeName, GetNextSequentialName());
				}

				return new DisplayNormalizedName(typeName, prefix);
			}

			return new DisplayNormalizedName(typeName, typeName);
		}

		string GetNextSequentialName() {
			var number = _nameId++;
			var bigLength = (long)alphaNumCharset.Length;
			_nameBuilder.Clear();
			do {
				number = Math.DivRem(number, bigLength, out var remainder);
				_nameBuilder.Append(alphaNumCharset[remainder]);
			} while (number != 0);
			return _nameBuilder.ToString();
		}
	}
}
