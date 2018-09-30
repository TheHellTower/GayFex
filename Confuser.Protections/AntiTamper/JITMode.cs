using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Confuser.Protections.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.AntiTamper {
	internal class JITMode : IModeHandler {
		static readonly CilBody NopBody = new CilBody {
			Instructions = {
				Instruction.Create(OpCodes.Ldnull),
				Instruction.Create(OpCodes.Throw)
			}
		};

		uint c;
		MethodDef cctor;
		MethodDef cctorRepl;
		IConfuserContext context;
		IKeyDeriver deriver;
		byte[] fieldLayout;

		MethodDef initMethod;
		uint key;
		List<MethodDef> methods;
		uint name1, name2;
		IRandomGenerator random;
		uint v;
		uint x;
		uint z;

		public void HandleInject(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters) {
			this.context = context;
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator(AntiTamperProtection._FullId);
			z = random.NextUInt32();
			x = random.NextUInt32();
			c = random.NextUInt32();
			v = random.NextUInt32();
			name1 = random.NextUInt32() & 0x7f7f7f7f;
			name2 = random.NextUInt32() & 0x7f7f7f7f;
			key = random.NextUInt32();

			switch (parameters.GetParameter(context, context.CurrentModule, parent.Parameters.Key)) {
				case KeyDeriverMode.Normal:
					deriver = new NormalDeriver();
					break;
				case KeyDeriverMode.Dynamic:
					deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}
			deriver.Init(context, random);

			var mutationKeys = ImmutableDictionary.Create<MutationField, int>()
				.Add(MutationField.KeyI0, (int)(name1 * name2))
				.Add(MutationField.KeyI1, (int)z)
				.Add(MutationField.KeyI2, (int)x)
				.Add(MutationField.KeyI3, (int)c)
				.Add(MutationField.KeyI4, (int)v)
				.Add(MutationField.KeyI5, (int)key);

			var name = context.Registry.GetRequiredService<INameService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var antiTamper = context.Registry.GetRequiredService<IAntiTamperService>();

			var antiTamperInitMethod = context.GetInitMethod("Confuser.Runtime.AntiTamperJIT", context.CurrentModule);
			if (antiTamperInitMethod == null) return;

			var injectResult = InjectHelper.Inject(antiTamperInitMethod, context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					KeyFieldValues = mutationKeys,
					CryptProcessor = deriver.EmitDerivation(context)
				});

			initMethod = injectResult.Requested.Mapped;

			cctor = context.CurrentModule.GlobalType.FindStaticConstructor();

			cctorRepl = new MethodDefUser(name.RandomName(), MethodSig.CreateStatic(context.CurrentModule.CorLibTypes.Void));
			cctorRepl.IsStatic = true;
			cctorRepl.Access = MethodAttributes.CompilerControlled;
			cctorRepl.Body = new CilBody();
			cctorRepl.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			context.CurrentModule.GlobalType.Methods.Add(cctorRepl);
			name.MarkHelper(context, cctorRepl, marker, parent);

			var methodDataMapping = new Dictionary<FieldDef, int>();
			foreach (var dependency in injectResult) {
				if (dependency.Source is FieldDef depField && depField.DeclaringType.Name == "MethodData") {
					var mapField = (FieldDef)dependency.Mapped;
					switch (depField.Name.String) {
						case "ILCodeSize": methodDataMapping.Add(mapField, 0); break;
						case "MaxStack": methodDataMapping.Add(mapField, 1); break;
						case "EHCount": methodDataMapping.Add(mapField, 2); break;
						case "LocalVars": methodDataMapping.Add(mapField, 3); break;
						case "Options": methodDataMapping.Add(mapField, 4); break;
						case "MulSeed": methodDataMapping.Add(mapField, 5); break;
					}
				}
			}

			foreach (var dependency in injectResult.InjectedDependencies) {
				if (dependency.Mapped is TypeDef mapType && dependency.Source.Name == "MethodData") {
					var fields = mapType.Fields.ToArray();
					random.Shuffle(fields);

					fieldLayout = new byte[fields.Length];
					for (var i = 0; i < fields.Length; i++) {
						fieldLayout[i] = (byte)methodDataMapping[fields[i]];
					}

					mapType.Fields.Clear();
					foreach (var field in fields)
						mapType.Fields.Add(field);

					break;
				}
			}

			foreach (var dep in injectResult) {
				name.MarkHelper(context, dep.Mapped, marker, parent);
				if (dep.Mapped is MethodDef methodDef)
					antiTamper.ExcludeMethod(context, methodDef);
			}

			antiTamper.ExcludeMethod(context, cctor);
		}

		public void HandleMD(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters) {
			// move initialization away from module initializer
			cctorRepl.Body = cctor.Body;
			cctor.Body = new CilBody();
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, initMethod));
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, cctorRepl));
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			methods = parameters.Targets.OfType<MethodDef>().Where(method => method.HasBody).ToList();
			context.CurrentModuleWriterOptions.WriterEvent += OnWriterEvent;
		}

		void OnWriterEvent(object sender, ModuleWriterEventArgs e) {
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("anti tamper");
			var writer = e.Writer;
			if (e.Event == ModuleWriterEvent.MDBeginWriteMethodBodies) {
				logger.Debug("Extracting method bodies...");
				CreateSection(writer);
			}
			else if (e.Event == ModuleWriterEvent.BeginStrongNameSign) {
				logger.Debug("Encrypting method section...");
				EncryptSection(writer);
			}
		}

		void CreateSection(ModuleWriterBase writer) {
			// move some PE parts to separate section to prevent it from being hashed
			var peSection = new PESection("", 0x60000020);
			bool moved = false;
			uint alignment;
			if (writer.StrongNameSignature != null) {
				alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
				peSection.Add(writer.StrongNameSignature, alignment);
				moved = true;
			}
			var managedWriter = writer as ModuleWriter;
			if (managedWriter != null) {
				if (managedWriter.ImportAddressTable != null) {
					alignment = writer.TextSection.Remove(managedWriter.ImportAddressTable).Value;
					peSection.Add(managedWriter.ImportAddressTable, alignment);
					moved = true;
				}
				if (managedWriter.StartupStub != null) {
					alignment = writer.TextSection.Remove(managedWriter.StartupStub).Value;
					peSection.Add(managedWriter.StartupStub, alignment);
					moved = true;
				}
			}
			if (moved)
				writer.Sections.AddBeforeReloc(peSection);

			// create section
			var nameBuffer = new byte[8];
			nameBuffer[0] = (byte)(name1 >> 0);
			nameBuffer[1] = (byte)(name1 >> 8);
			nameBuffer[2] = (byte)(name1 >> 16);
			nameBuffer[3] = (byte)(name1 >> 24);
			nameBuffer[4] = (byte)(name2 >> 0);
			nameBuffer[5] = (byte)(name2 >> 8);
			nameBuffer[6] = (byte)(name2 >> 16);
			nameBuffer[7] = (byte)(name2 >> 24);
			var newSection = new PESection(Encoding.ASCII.GetString(nameBuffer), 0xE0000040);
			writer.Sections.InsertBeforeReloc(random.NextInt32(writer.Sections.Count), newSection);

			// random padding at beginning to prevent revealing hash key
			newSection.Add(new ByteArrayChunk(random.NextBytes(0x10).ToArray()), 0x10);

			// create index
			var bodyIndex = new JITBodyIndex(methods.Select(method => writer.Metadata.GetToken(method).Raw));
			newSection.Add(bodyIndex, 0x10);

			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("anti tamper");

			// save methods
			foreach (var method in methods.WithProgress(logger)) {
				if (!method.HasBody)
					continue;

				var token = writer.Metadata.GetToken(method);

				var jitBody = new JITMethodBody();
				var bodyWriter = new JITMethodBodyWriter(writer.Metadata, method.Body, jitBody, random.NextUInt32(), writer.Metadata.KeepOldMaxStack || method.Body.KeepOldMaxStack);
				bodyWriter.Write();
				jitBody.Serialize(token.Raw, key, fieldLayout);
				bodyIndex.Add(token.Raw, jitBody);

				method.Body = NopBody;
				var methodRow = writer.Metadata.TablesHeap.MethodTable[token.Rid];
				writer.Metadata.TablesHeap.MethodTable[token.Rid] = new RawMethodRow(
					methodRow.RVA,
					(ushort)(methodRow.ImplFlags | (ushort)MethodImplAttributes.NoInlining),
					methodRow.Flags,
					methodRow.Name,
					methodRow.Signature,
					methodRow.ParamList);
			}
			bodyIndex.PopulateSection(newSection);

			// padding to prevent bad size due to shift division
			newSection.Add(new ByteArrayChunk(new byte[4]), 4);
		}

		void EncryptSection(ModuleWriterBase writer) {
			var stream = writer.DestinationStream;
			var reader = new BinaryReader(writer.DestinationStream);
			stream.Position = 0x3C;
			stream.Position = reader.ReadUInt32();

			stream.Position += 6;
			ushort sections = reader.ReadUInt16();
			stream.Position += 0xc;
			ushort optSize = reader.ReadUInt16();
			stream.Position += 2 + optSize;

			uint encLoc = 0, encSize = 0;
			int origSects = -1;
			if (writer is NativeModuleWriter && writer.Module is ModuleDefMD)
				origSects = ((ModuleDefMD)writer.Module).Metadata.PEImage.ImageSectionHeaders.Count;
			for (int i = 0; i < sections; i++) {
				uint nameHash;
				if (origSects > 0) {
					origSects--;
					stream.Write(new byte[8], 0, 8);
					nameHash = 0;
				}
				else
					nameHash = reader.ReadUInt32() * reader.ReadUInt32();
				stream.Position += 8;
				if (nameHash == name1 * name2) {
					encSize = reader.ReadUInt32();
					encLoc = reader.ReadUInt32();
				}
				else if (nameHash != 0) {
					uint sectSize = reader.ReadUInt32();
					uint sectLoc = reader.ReadUInt32();
					Hash(stream, reader, sectLoc, sectSize);
				}
				else
					stream.Position += 8;
				stream.Position += 16;
			}

			uint[] key = DeriveKey();
			encSize >>= 2;
			stream.Position = encLoc;
			var result = new uint[encSize];
			for (uint i = 0; i < encSize; i++) {
				uint data = reader.ReadUInt32();
				result[i] = data ^ key[i & 0xf];
				key[i & 0xf] = (key[i & 0xf] ^ data) + 0x3dbb2819;
			}
			var byteResult = new byte[encSize << 2];
			Buffer.BlockCopy(result, 0, byteResult, 0, byteResult.Length);
			stream.Position = encLoc;
			stream.Write(byteResult, 0, byteResult.Length);
		}

		void Hash(Stream stream, BinaryReader reader, uint offset, uint size) {
			long original = stream.Position;
			stream.Position = offset;
			size >>= 2;
			for (uint i = 0; i < size; i++) {
				uint data = reader.ReadUInt32();
				uint tmp = (z ^ data) + x + c * v;
				z = x;
				x = c;
				x = v;
				v = tmp;
			}
			stream.Position = original;
		}

		uint[] DeriveKey() {
			uint[] dst = new uint[0x10], src = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				dst[i] = v;
				src[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}
			return deriver.DeriveKey(dst, src);
		}
	}
}
