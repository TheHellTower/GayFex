using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Properties;
using dnlib.DotNet;

namespace Confuser.Renamer {
	public interface INameService {
		VTableStorage GetVTables();

		void Analyze(IDnlibDef def);

		bool CanRename(object obj);
		void SetCanRename(object obj, bool val);

		void SetParam(IDnlibDef def, string name, string value);
		string GetParam(IDnlibDef def, string name);

		RenameMode GetRenameMode(object obj);
		void SetRenameMode(object obj, RenameMode val);
		void ReduceRenameMode(object obj, RenameMode val);

		string ObfuscateName(string name, RenameMode mode);
		string ObfuscateName(IDnlibDef name, RenameMode mode);
		string RandomName();
		string RandomName(RenameMode mode);

		void RegisterRenamer(IRenamer renamer);
		T FindRenamer<T>();
		void AddReference<T>(T obj, INameReference<T> reference);
		IList<INameReference> GetReferences(object obj);

		void SetOriginalName(IDnlibDef obj, string fullName = null);
		string GetOriginalFullName(IDnlibDef obj);

		bool IsRenamed(IDnlibDef def);
		void SetIsRenamed(IDnlibDef def);

		void MarkHelper(IDnlibDef def, IMarkerService marker, ConfuserComponent parentComp);
	}

	internal class NameService : INameService {
		static readonly object CanRenameKey = new object();
		static readonly object RenameModeKey = new object();
		static readonly object ReferencesKey = new object();
		static readonly object OriginalFullNameKey = new object();
		static readonly object IsRenamedKey = new object();

		readonly ConfuserContext context;
		readonly byte[] nameSeed;
		readonly RandomGenerator random;
		readonly VTableStorage storage;
		AnalyzePhase analyze;

		readonly HashSet<string> identifiers = new HashSet<string>();

		readonly byte[] nameId = new byte[8];
		readonly Dictionary<string, string> _originalToObfuscatedNameMap = new Dictionary<string, string>();
		readonly Dictionary<string, string> _obfuscatedToOriginalNameMap = new Dictionary<string, string>();
		internal ReversibleRenamer reversibleRenamer;

		public NameService(ConfuserContext context) {
			this.context = context;
			storage = new VTableStorage(context.Logger);
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator(NameProtection._FullId);
			nameSeed = random.NextBytes(20);

			Renamers = new List<IRenamer> {
				new InterReferenceAnalyzer(),
				new VTableAnalyzer(),
				new TypeBlobAnalyzer(),
				new ResourceAnalyzer(),
				new LdtokenEnumAnalyzer(),
				new ManifestResourceAnalyzer(),
				new ReflectionAnalyzer(),
				new CallSiteAnalyzer()
			};
		}

		public IList<IRenamer> Renamers { get; private set; }

		public VTableStorage GetVTables() {
			return storage;
		}

		public bool CanRename(object obj) {
			if (obj is IDnlibDef) {
				if (analyze == null)
					analyze = context.Pipeline.FindPhase<AnalyzePhase>();

				var prot = (NameProtection)analyze.Parent;
				ProtectionSettings parameters = ProtectionParameters.GetParameters(context, (IDnlibDef)obj);
				if (parameters == null || !parameters.ContainsKey(prot))
					return false;
				return context.Annotations.Get(obj, CanRenameKey, true);
			}
			return false;
		}

		public void SetCanRename(object obj, bool val) {
			context.Annotations.Set(obj, CanRenameKey, val);
		}

		public void SetParam(IDnlibDef def, string name, string value) {
			var param = ProtectionParameters.GetParameters(context, def);
			if (param == null)
				ProtectionParameters.SetParameters(context, def, param = new ProtectionSettings());
			Dictionary<string, string> nameParam;
			if (!param.TryGetValue(analyze.Parent, out nameParam))
				param[analyze.Parent] = nameParam = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			nameParam[name] = value;
		}

		public string GetParam(IDnlibDef def, string name) {
			var param = ProtectionParameters.GetParameters(context, def);
			if (param == null)
				return null;
			if (analyze == null)
				analyze = context.Pipeline.FindPhase<AnalyzePhase>();
			if (!param.TryGetValue(analyze.Parent, out var nameParam))
				return null;
			return nameParam.GetValueOrDefault(name);
		}

		public RenameMode GetRenameMode(object obj) {
			return context.Annotations.Get(obj, RenameModeKey, RenameMode.Unicode);
		}

		public void SetRenameMode(object obj, RenameMode val) {
			context.Annotations.Set(obj, RenameModeKey, val);
		}

		public void ReduceRenameMode(object obj, RenameMode val) {
			RenameMode original = GetRenameMode(obj);
			if (original < val)
				context.Annotations.Set(obj, RenameModeKey, val);
			if (val <= RenameMode.Reflection && obj is IDnlibDef dnlibDef) {
				string nameWithoutParams = GetSimplifiedFullName(dnlibDef, true);
				SetOriginalName(dnlibDef, nameWithoutParams);
			}
		}

		public void AddReference<T>(T obj, INameReference<T> reference) {
			context.Annotations.GetOrCreate(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
		}

		public void Analyze(IDnlibDef def) {
			if (analyze == null)
				analyze = context.Pipeline.FindPhase<AnalyzePhase>();

			SetOriginalName(def);
			if (def is TypeDef typeDef) {
				GetVTables().GetVTable(typeDef);
			}
			analyze.Analyze(this, context, ProtectionParameters.Empty, def, true);
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
				case RenameMode.Reflection:
					return Utils.EncodeString(hash, reflectionCharset);
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

		public string ObfuscateName(IDnlibDef dnlibDef, RenameMode mode) {
			var originalFullName = GetOriginalFullName(dnlibDef);
			bool preserveGenericParams = GetParam(dnlibDef, "preserveGenericParams")
				?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
			return ObfuscateName(null, originalFullName, mode, preserveGenericParams);
		}

		public string ObfuscateName(string format, string name, RenameMode mode, bool preserveGenericParams = false) {
			int genericParamsCount = 0;
			if (preserveGenericParams) {
				name = ParseGenericName(name, out genericParamsCount);
			}

			string newName;

			if (string.IsNullOrEmpty(name) || mode == RenameMode.Empty)
				return string.Empty;

			if (mode == RenameMode.Debug || mode == RenameMode.Retain)
			{
				// When flattening there are issues, in case there is a . in the name of the assembly.
				newName = name.Replace('.', '_');
				newName = mode == RenameMode.Debug ? "_" + newName : newName;
			}
			else if (mode == RenameMode.Reversible)
			{
				if (reversibleRenamer == null)
					throw new ArgumentException("Password not provided for reversible renaming.");
				newName = reversibleRenamer.Encrypt(name);
			}
			else if (!_originalToObfuscatedNameMap.TryGetValue(name, out newName))
			{
				byte[] hash = Utils.Xor(Utils.SHA1(Encoding.UTF8.GetBytes(name)), nameSeed);
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
			return ObfuscateName(Utils.ToHexString(random.NextBytes(16)), mode);
		}

		public void SetOriginalName(IDnlibDef dnlibDef, string newFullName = null) {
			AddReservedIdentifier(dnlibDef.Name);
			if (dnlibDef is TypeDef typeDef) {
				AddReservedIdentifier(typeDef.Namespace);
			}
			string fullName = newFullName ?? GetSimplifiedFullName(dnlibDef);
			context.Annotations.Set(dnlibDef, OriginalFullNameKey, fullName);
		}

		public void AddReservedIdentifier(string id) => identifiers.Add(id);

		public void RegisterRenamer(IRenamer renamer) {
			Renamers.Add(renamer);
		}

		public T FindRenamer<T>() {
			return Renamers.OfType<T>().Single();
		}

		public void MarkHelper(IDnlibDef def, IMarkerService marker, ConfuserComponent parentComp) {
			if (marker.IsMarked(def))
				return;
			if (def is MethodDef) {
				var method = (MethodDef)def;
				method.Access = MethodAttributes.Assembly;
				if (!method.IsSpecialName && !method.IsRuntimeSpecialName && !method.DeclaringType.IsDelegate())
					method.Name = RandomName();
			}
			else if (def is FieldDef) {
				var field = (FieldDef)def;
				field.Access = FieldAttributes.Assembly;
				if (!field.IsSpecialName && !field.IsRuntimeSpecialName)
					field.Name = RandomName();
			}
			else if (def is TypeDef) {
				var type = (TypeDef)def;
				type.Visibility = type.DeclaringType == null ? TypeAttributes.NotPublic : TypeAttributes.NestedAssembly;
				type.Namespace = "";
				if (!type.IsSpecialName && !type.IsRuntimeSpecialName)
					type.Name = RandomName();
			}
			SetCanRename(def, false);
			Analyze(def);
			marker.Mark(def, parentComp);
		}

		#region Charsets

		static readonly char[] asciiCharset = Enumerable.Range(32, 95)
		                                                .Select(ord => (char)ord)
		                                                .Except(new[] { '.' })
		                                                .ToArray();

		static readonly char[] reflectionCharset = asciiCharset.Except(new[] { ' ', '[', ']' }).ToArray();

		static readonly char[] letterCharset = Enumerable.Range(0, 26)
		                                                 .SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) })
		                                                 .ToArray();

		static readonly char[] alphaNumCharset = Enumerable.Range(0, 26)
		                                                   .SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) })
		                                                   .Concat(Enumerable.Range(0, 10).Select(ord => (char)('0' + ord)))
		                                                   .ToArray();

		// Especially chosen, just to mess with people.
		// Inspired by: http://xkcd.com/1137/ :D
		static readonly char[] unicodeCharset = new char[] { }
			.Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x2029, 6).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
			.Except(new[] { '\u2029' })
			.ToArray();

		#endregion

		public RandomGenerator GetRandom() {
			return random;
		}

		public IList<INameReference> GetReferences(object obj) {
			return context.Annotations.GetLazy(obj, ReferencesKey, key => new List<INameReference>());
		}

		public string GetOriginalFullName(IDnlibDef obj) =>
			context.Annotations.Get(obj, OriginalFullNameKey, (string)null) ?? GetSimplifiedFullName(obj);

		public IReadOnlyDictionary<string, string> GetNameMap() => _obfuscatedToOriginalNameMap;

		public bool IsRenamed(IDnlibDef def) => context.Annotations.Get(def, IsRenamedKey, !CanRename(def));

		public void SetIsRenamed(IDnlibDef def) => context.Annotations.Set(def, IsRenamedKey, true);

		string GetSimplifiedFullName(IDnlibDef dnlibDef, bool forceShortNames = false) {
			string result;

			var shortNames = forceShortNames ||
			                 GetParam(dnlibDef, "shortNames")?.Equals("true", StringComparison.OrdinalIgnoreCase) ==
			                 true;
			if (shortNames) {
				result = dnlibDef is MethodDef ? (string)dnlibDef.Name : dnlibDef.FullName;
			}
			else {
				if (dnlibDef is MethodDef methodDef) {
					var resultBuilder = new StringBuilder();
					resultBuilder.Append(methodDef.DeclaringType2?.FullName);
					resultBuilder.Append("::");
					resultBuilder.Append(dnlibDef.Name);

					resultBuilder.Append('(');
					if (methodDef.Signature is MethodSig methodSig) {
						var methodParams = methodSig.Params;
						for (var index = 0; index < methodParams.Count; index++) {
							resultBuilder.Append(methodParams[index]);
							if (index < methodParams.Count - 1) {
								resultBuilder.Append(',');
							}
						}
					}
					resultBuilder.Append(')');

					result = resultBuilder.ToString();
				}
				else {
					result = dnlibDef.FullName;
				}
			}

			return result;
		}
	}
}
