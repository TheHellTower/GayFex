using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.AntiTamper {
	internal class NormalMode : IModeHandler {
		// ReSharper disable InconsistentNaming
		protected const uint CNT_CODE = 0x20;
		protected const uint CNT_INITIALIZED_DATA = 0x40;
		protected const uint MEM_EXECUTE = 0x20000000;
		protected const uint MEM_READ = 0x40000000;

		protected const uint MEM_WRITE = 0x80000000;
		// ReSharper restore InconsistentNaming

		/// <summary>The deriver of the key that is used to encrypt the bodies of the protected methods.</summary>
		private IKeyDeriver _deriver;

		/// <summary>The name of the section split into two 4 byte parts.</summary>
		private (uint Part1, uint Part2) _sectionName;

		/// <summary>The components of the key that is used to detect tampering.</summary>
		private uint _c;

		private uint _v;
		private uint _x;
		private uint _z;

		/// <summary>The methods that will to protected against tampering.</summary>
		protected IImmutableList<MethodDef> Methods { get; private set; }

		void IModeHandler.HandleInject(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters) => HandleInject(parent, context, parameters);

		protected virtual void HandleInject(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters) {
			var logger = context.Registry.GetService<ILoggerProvider>().CreateLogger(AntiTamperProtection._Id);
			logger.LogMsgNormalModeStart(context.CurrentModule);

			var random = context.Registry.GetRequiredService<IRandomService>()
				.GetRandomGenerator(AntiTamperProtection._FullId);
			InitParameters(parent, context, parameters, random);

			var injectResult = InjectRuntime(parent, context, "Confuser.Runtime.AntiTamperNormal");
			if (injectResult == null) {
				logger.LogMsgNormalModeRuntimeMissing();
				return;
			}

			var antiTamper = context.Registry.GetRequiredService<IAntiTamperService>();

			var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
			cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));
			antiTamper.ExcludeMethod(context, cctor);

			logger.LogMsgNormalModeInjectDone(context.CurrentModule);
		}

		protected virtual void InitParameters(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters, IRandomGenerator random) {
			_z = random.NextUInt32();
			_x = random.NextUInt32();
			_c = random.NextUInt32();
			_v = random.NextUInt32();

			_sectionName = (random.NextUInt32() & 0x7f7f7f7f, random.NextUInt32() & 0x7f7f7f7f);

			switch (parameters.GetParameter(context, context.CurrentModule, parent.Parameters.Key)) {
				case KeyDeriverMode.Normal:
					_deriver = new NormalDeriver();
					break;
				case KeyDeriverMode.Dynamic:
					_deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}

			_deriver.Init(context, random);
		}

		protected InjectResult<MethodDef> InjectRuntime(AntiTamperProtection parent, IConfuserContext context,
			string runtimeTypeName) {
			var logger = context.Registry.GetService<ILoggerProvider>().CreateLogger(AntiTamperProtection._Id);
			var mutationKeys = CreateMutationKeys();

			var name = context.Registry.GetService<INameService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var antiTamper = context.Registry.GetRequiredService<IAntiTamperService>();

			logger.LogMsgNormalModeInjectStart(context.CurrentModule);

			var antiTamperInit = context.GetInitMethod(runtimeTypeName, context.CurrentModule);
			if (antiTamperInit == null) return null;

			var injectHelper = context.Registry.GetRequiredService<ProtectionsRuntimeService>().InjectHelper;

			var injectResult = injectHelper.Inject(antiTamperInit, context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					KeyFieldValues = mutationKeys,
					CryptProcessor = _deriver.EmitDerivation(context)
				});

			foreach (var (_, mapped) in injectResult) {
				name?.MarkHelper(context, mapped, marker, parent);
				if (mapped is MethodDef methodDef)
					antiTamper.ExcludeMethod(context, methodDef);
			}

			return injectResult;
		}

		protected virtual IImmutableDictionary<MutationField, int> CreateMutationKeys() =>
			ImmutableDictionary.Create<MutationField, int>()
				.Add(MutationField.KeyI0, (int)(_sectionName.Part1 * _sectionName.Part2))
				.Add(MutationField.KeyI1, (int)_z)
				.Add(MutationField.KeyI2, (int)_x)
				.Add(MutationField.KeyI3, (int)_c)
				.Add(MutationField.KeyI4, (int)_v);

		void IModeHandler.HandleMD(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters) => HandleMD(parent, context, parameters);

		protected virtual void HandleMD(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters) {
			Methods = parameters.Targets.OfType<MethodDef>().ToImmutableList();
			context.CurrentModuleWriterOptions.WriterEvent += WriterEvent;
		}

		private void WriterEvent(object sender, ModuleWriterEventArgs e) {
			switch (e.Event)
			{
				case ModuleWriterEvent.MDEndCreateTables:
					CreateSections(e.Writer);
					break;
				case ModuleWriterEvent.BeginStrongNameSign:
					EncryptSection(e.Writer);
					break;
			}
		}

		protected string CreateEncryptedSectionName() {
			var nameBuffer = ArrayPool<byte>.Shared.Rent(8);
			try {
				nameBuffer[0] = (byte)(_sectionName.Part1 >> 0);
				nameBuffer[1] = (byte)(_sectionName.Part1 >> 8);
				nameBuffer[2] = (byte)(_sectionName.Part1 >> 16);
				nameBuffer[3] = (byte)(_sectionName.Part1 >> 24);
				nameBuffer[4] = (byte)(_sectionName.Part2 >> 0);
				nameBuffer[5] = (byte)(_sectionName.Part2 >> 8);
				nameBuffer[6] = (byte)(_sectionName.Part2 >> 16);
				nameBuffer[7] = (byte)(_sectionName.Part2 >> 24);
				return Encoding.ASCII.GetString(nameBuffer, 0, 8);
			}
			finally {
				ArrayPool<byte>.Shared.Return(nameBuffer);
			}
		}

		[SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
		protected virtual void CreateSections(ModuleWriterBase writer) {
			var newSection = new PESection(
				CreateEncryptedSectionName(),
				CNT_INITIALIZED_DATA | MEM_EXECUTE | MEM_READ | MEM_WRITE);
			writer.Sections.Insert(0, newSection); // insert first to ensure proper RVA

			var alignment = writer.TextSection.Remove(writer.Metadata).Value;
			writer.TextSection.Add(writer.Metadata, alignment);

			alignment = writer.TextSection.Remove(writer.NetResources).Value;
			writer.TextSection.Add(writer.NetResources, alignment);

			alignment = writer.TextSection.Remove(writer.Constants).Value;
			newSection.Add(writer.Constants, alignment);

			// move some PE parts to separate section to prevent it from being hashed
			var peSection = new PESection("", CNT_CODE | MEM_EXECUTE | MEM_READ);
			bool moved = false;
			if (writer.StrongNameSignature != null) {
				alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
				peSection.Add(writer.StrongNameSignature, alignment);
				moved = true;
			}

			if (writer is ModuleWriter managedWriter) {
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

			// move encrypted methods
			var encryptedChunk = new MethodBodyChunks(writer.TheOptions.ShareMethodBodies);
			newSection.Add(encryptedChunk, 4);
			foreach (var method in Methods) {
				if (!method.HasBody) continue;
				var body = writer.Metadata.GetMethodBody(method);
				writer.MethodBodies.Remove(body);
				encryptedChunk.Add(body);
			}

			// padding to prevent bad size due to shift division
			newSection.Add(new ByteArrayChunk(new byte[4]), 4);
		}

		private void EncryptSection(ModuleWriterBase writer) {
			var stream = writer.DestinationStream;
			var reader = new BinaryReader(stream);
			stream.Position = 0x3C; // DOS-HEADER: Byte position of file header start (e_lfanew) (DWORD)
			stream.Position = reader.ReadUInt32();

			stream.Position += 6; // IMAGE_FILE_HEADER: NumberOfSections (WORD)
			ushort sections = reader.ReadUInt16();
			stream.Position += 0xc; // IMAGE_FILE_HEADER: SizeOfOptionalHeader (WORD)
			ushort optSize = reader.ReadUInt16();
			stream.Position += 2 + optSize; // Skip characteristics and optional header

			uint encLoc = 0, encSize = 0;
			int origSects = -1;

			// Get the amount of sections that were originally present in the image.
			if (writer is NativeModuleWriter && writer.Module is ModuleDefMD moduleDefMd)
				origSects = moduleDefMd.Metadata.PEImage.ImageSectionHeaders.Count;


			for (int i = 0; i < sections; i++) {
				// Read one section after another and skip all the original PE headers
				uint nameHash;
				if (origSects > 0) {
					// One of the original sections. Remove the name!
					origSects--;
					stream.Write(new byte[8], 0, 8);
					nameHash = 0;
				}
				else {
					// The name of a section (like .text or .rsrc) is always represented by 8 byte
					nameHash = reader.ReadUInt32() * reader.ReadUInt32();
				}

				stream.Position += 8; // Forward to SizeOfRawData field
				if (nameHash == _sectionName.Part1 * _sectionName.Part2) {
					// This section is the section with the encrypted data.
					encSize = reader.ReadUInt32(); // SizeOfRawData (DWORD)
					encLoc = reader.ReadUInt32(); // PointerToRawData (DWORD)
				}
				else if (nameHash != 0) {
					// Not the encrypted section, but a section with a name. The raw data of the section is used to
					// transform the encryption code. So if anyone modifies the code, the module with the method bodies
					// can't be decoded.
					uint sectSize = reader.ReadUInt32(); // SizeOfRawData (DWORD)
					uint sectLoc = reader.ReadUInt32(); // PointerToRawData (DWORD)
					Hash(stream, reader, sectLoc, sectSize);
				}
				else {
					// Skip the size and the pointer field
					stream.Position += 8;
				}

				// Move to start of next section or to IMAGE_COR20_HEADER
				stream.Position += 16;
			}

			// Create the key from the hash values.
			var key = DeriveKey();
			encSize >>= 2;
			stream.Position = encLoc;

			// Transform the sensitive section using the key.
			var result = new uint[encSize];
			for (uint i = 0; i < encSize; i++) {
				uint data = reader.ReadUInt32();
				result[i] = data ^ key[i & 0xf];
				key[i & 0xf] = (key[i & 0xf] ^ data) + 0x3dbb2819;
			}

			// Overwrite the sensitive section with the transformed data.
			var byteResult = new byte[encSize << 2];
			Buffer.BlockCopy(result, 0, byteResult, 0, byteResult.Length);
			stream.Position = encLoc;
			stream.Write(byteResult, 0, byteResult.Length);
		}

		private void Hash(Stream stream, BinaryReader reader, uint offset, uint size) {
			long original = stream.Position;
			stream.Position = offset;
			size >>= 2;
			for (uint i = 0; i < size; i++) {
				uint data = reader.ReadUInt32();
				uint tmp = (_z ^ data) + _x + _c * _v;
				_z = _x;
				_x = _c;
				_c = _v;
				_v = tmp;
			}

			stream.Position = original;
		}

		private uint[] DeriveKey() {
			uint[] dst = new uint[0x10], src = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				dst[i] = _v;
				src[i] = _x;
				_z = (_x >> 5) | (_x << 27);
				_x = (_c >> 3) | (_c << 29);
				_c = (_v >> 7) | (_v << 25);
				_v = (_z >> 11) | (_z << 21);
			}

			return _deriver.DeriveKey(dst, src);
		}
	}
}
