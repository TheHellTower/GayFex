using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer.Services {
	internal class NameService : INameService {
		static readonly object CanRenameKey = new object();
		static readonly object RenameModeKey = new object();
		static readonly object ReferencesKey = new object();
		static readonly object OriginalNameKey = new object();
		static readonly object OriginalNamespaceKey = new object();

		private readonly ReadOnlyMemory<byte> nameSeed;
		readonly IRandomGenerator random;
		readonly VTableStorage storage;
		private readonly NameProtection _parent;

		readonly HashSet<string> identifiers = new HashSet<string>();
		readonly byte[] nameId = new byte[8];
		readonly Dictionary<string, string> nameMap1 = new Dictionary<string, string>();
		readonly Dictionary<string, string> nameMap2 = new Dictionary<string, string>();
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
				new LdtokenEnumAnalyzer()
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

		public void SetParam(IConfuserContext context, IDnlibDef def, string name, string value) {
			context.GetParameters(def).SetParameter(_parent, name, value);
		}

		public string GetParam(IConfuserContext context, IDnlibDef def, string name) {
			return context.GetParameters(def).GetParameter(_parent, name);
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
		}

		public void AddReference<T>(IConfuserContext context, T obj, INameReference<T> reference) {
			context.Annotations.GetOrCreate(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
		}

		public void Analyze(IConfuserContext context, IDnlibDef def) {
			var analyze = context.Pipeline.FindPhase<AnalyzePhase>();

			SetOriginalName(context, def, def.Name);
			if (def is TypeDef) {
				GetVTables().GetVTable((TypeDef)def);
				SetOriginalNamespace(context, def, ((TypeDef)def).Namespace);
			}

			analyze.Analyze(this, context, EmptyProtectionParameters.Instance, def, true);
		}

		public void SetNameId(uint id) {
			for (int i = nameId.Length - 1; i >= 0; i--) {
				nameId[i] = (byte)(id & 0xff);
				id >>= 8;
			}
		}

		void IncrementNameId() {
			for (int i = nameId.Length - 1; i >= 0; i--) {
				nameId[i]++;
				if (nameId[i] != 0)
					break;
			}
		}

		string ObfuscateNameInternal(byte[] hash, RenameMode mode) {
			switch (mode) {
				case RenameMode.Empty:
					return "";
				case RenameMode.Unicode:
					return Utils.EncodeString(hash, unicodeCharset) + "\u202e";
				case RenameMode.Letters:
					return Utils.EncodeString(hash, letterCharset);
				case RenameMode.ASCII:
					return Utils.EncodeString(hash, asciiCharset);
				case RenameMode.Decodable:
					IncrementNameId();
					return "_" + Utils.EncodeString(hash, alphaNumCharset);
				case RenameMode.Sequential:
					IncrementNameId();
					return "_" + Utils.EncodeString(nameId, alphaNumCharset);
				default:

					throw new NotSupportedException("Rename mode '" + mode + "' is not supported.");
			}
		}

		string ParseGenericName(string name, out int? count) {
			if (name.LastIndexOf('`') != -1) {
				int index = name.LastIndexOf('`');
				int c;
				if (int.TryParse(name.Substring(index + 1), out c)) {
					count = c;
					return name.Substring(0, index);
				}
			}

			count = null;
			return name;
		}

		string MakeGenericName(string name, int? count) {
			if (count == null)
				return name;
			else
				return string.Format("{0}`{1}", name, count.Value);
		}

		string INameService.ObfuscateName(ModuleDef module, string name, RenameMode mode) {
			if (module == null) throw new ArgumentNullException(nameof(module));

			return ObfuscateName(name, mode);
		}

		public string ObfuscateName(string name, RenameMode mode) {
			return ObfuscateName(null, name, mode);
		}

		public string ObfuscateName(ModuleDef module, string name, RenameMode mode) {
			string newName = null;
			int? count;
			name = ParseGenericName(name, out count);

			if (string.IsNullOrEmpty(name))
				return string.Empty;

			if (mode == RenameMode.Empty)
				return "";
			if (mode == RenameMode.Debug)
				return "_" + name;
			if (mode == RenameMode.Reversible) {
				if (reversibleRenamer == null)
					throw new ArgumentException("Password not provided for reversible renaming.");
				newName = reversibleRenamer.Encrypt(name);
				return MakeGenericName(newName, count);
			}

			if (nameMap1.ContainsKey(name))
				return MakeGenericName(nameMap1[name], count);

			byte[] hash = Utils.Xor(Utils.SHA1(Encoding.UTF8.GetBytes(name)), nameSeed.ToArray());
			for (int i = 0; i < 100; i++) {
				newName = ObfuscateNameInternal(hash, mode);
				if (!identifiers.Contains(MakeGenericName(newName, count)))
					break;
				hash = Utils.SHA1(hash);
			}

			if ((mode & RenameMode.Decodable) != 0) {
				nameMap2[newName] = name;
				nameMap1[name] = newName;
			}

			return MakeGenericName(newName, count);
		}

		public string RandomName() {
			return RandomName(RenameMode.Unicode);
		}

		public string RandomName(RenameMode mode) {
			Span<byte> buf = stackalloc byte[16];
			random.NextBytes(buf);
			return ObfuscateName(Utils.ToHexString(buf), mode);
		}

		public void SetOriginalName(IConfuserContext context, object obj, string name) {
			identifiers.Add(name);
			context.Annotations.Set(obj, OriginalNameKey, name);
		}

		public void SetOriginalNamespace(IConfuserContext context, object obj, string ns) {
			identifiers.Add(ns);
			context.Annotations.Set(obj, OriginalNamespaceKey, ns);
		}

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

		public string GetOriginalName(IConfuserContext context, object obj) {
			return context.Annotations.Get(obj, OriginalNameKey, "");
		}

		public string GetOriginalNamespace(IConfuserContext context, object obj) {
			return context.Annotations.Get(obj, OriginalNamespaceKey, "");
		}

		public IReadOnlyCollection<KeyValuePair<string, string>> GetNameMap(ModuleDef module) => nameMap2;
	}
}
