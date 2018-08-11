using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	internal sealed class InvalidMetadataProtectionPhase : IProtectionPhase {
		IRandomGenerator random;

		public InvalidMetadataProtectionPhase(InvalidMetadataProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public InvalidMetadataProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Invalid metadata addition";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (parameters.Targets.Contains(context.CurrentModule)) {
				random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(InvalidMetadataProtection._FullId);
				context.CurrentModuleWriterOptions.WriterEvent += OnWriterEvent;
				token.ThrowIfCancellationRequested();
			}
		}

		void Randomize<T>(MDTable<T> table) where T : struct => random.Shuffle(table);

		void OnWriterEvent(object sender, ModuleWriterEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.Event == ModuleWriterEvent.MDEndCreateTables) {
				// These hurts reflection

				/*
				uint methodLen = (uint)writer.MetaData.TablesHeap.MethodTable.Rows + 1;
				uint fieldLen = (uint)writer.MetaData.TablesHeap.FieldTable.Rows + 1;

				var root = writer.MetaData.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
						0, 0x7fff7fff, 0, 0x3FFFD, fieldLen, methodLen));
				writer.MetaData.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, root));

				var namespaces = writer.MetaData.TablesHeap.TypeDefTable
					.Select(row => row.Namespace)
					.Distinct()
					.ToList();
				foreach (var ns in namespaces)
				{
					if (ns == 0) continue;
					var type = writer.MetaData.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
						0, 0, ns, 0x3FFFD, fieldLen, methodLen));
					writer.MetaData.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, type));
				}

				foreach (var row in writer.MetaData.TablesHeap.ParamTable)
					row.Name = 0x7fff7fff;
				*/

				writer.Metadata.TablesHeap.ModuleTable.Add(new RawModuleRow(0, 0x7fff7fff, 0, 0, 0));
				writer.Metadata.TablesHeap.AssemblyTable.Add(new RawAssemblyRow(0, 0, 0, 0, 0, 0, 0, 0x7fff7fff, 0));

				int r = random.NextInt32(8, 16);
				for (int i = 0; i < r; i++)
					writer.Metadata.TablesHeap.ENCLogTable.Add(new RawENCLogRow(random.NextUInt32(), random.NextUInt32()));
				r = random.NextInt32(8, 16);
				for (int i = 0; i < r; i++)
					writer.Metadata.TablesHeap.ENCMapTable.Add(new RawENCMapRow(random.NextUInt32()));

				//Randomize(writer.MetaData.TablesHeap.NestedClassTable);
				Randomize(writer.Metadata.TablesHeap.ManifestResourceTable);
				//Randomize(writer.MetaData.TablesHeap.GenericParamConstraintTable);

				writer.TheOptions.MetadataOptions.TablesHeapOptions.ExtraData = random.NextUInt32();
				writer.TheOptions.MetadataOptions.TablesHeapOptions.UseENC = false;
				writer.TheOptions.MetadataOptions.MetadataHeaderOptions.VersionString += "\0\0\0\0";

				/*
				We are going to create a new specific '#GUID' Heap to avoid UnConfuserEX to work.
				<sarcasm>UnConfuserEX is so well coded, it relies on static cmp between values</sarcasm>
				If you deobfuscate this tool, you can see that it check for #GUID size and compare it to
				'16', so we have to create a new array of byte wich size is exactly 16 and put it into
				our brand new Heap
				*/
				//
				writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#GUID", Guid.NewGuid().ToByteArray()));
				//
				writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Strings", new byte[1]));
				writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Blob", new byte[1]));
				writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Schema", new byte[1]));
			}
			else if (e.Event == ModuleWriterEvent.MDOnAllTablesSorted) {
				writer.Metadata.TablesHeap.DeclSecurityTable.Add(new RawDeclSecurityRow(
																	 unchecked(0x7fff), 0xffff7fff, 0xffff7fff));
				/*
				writer.MetaData.TablesHeap.ManifestResourceTable.Add(new RawManifestResourceRow(
					0x7fff7fff, (uint)ManifestResourceAttributes.Private, 0x7fff7fff, 2));
				*/
			}
		}
		private sealed class RawHeap : HeapBase {
			readonly byte[] content;

			public RawHeap(string name, byte[] content) {
				Name = name;
				this.content = content;
			}

			public override string Name { get; }

			public override uint GetRawLength() => (uint)content.Length;

			protected override void WriteToImpl(DataWriter writer) => writer.WriteBytes(content);
		}
	}

}
