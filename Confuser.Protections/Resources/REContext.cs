using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Protections.Resources {
	internal sealed class REContext {
		public IConfuserContext Context;

		public FieldDef DataField;
		public TypeDef DataType;
		public IDynCipherService DynCipher;
		public MethodDef InitMethod;
		public IMarkerService Marker;

		public Mode Mode;

		public IEncodeMode ModeHandler;
		public ModuleDef Module;
		public INameService Name;
		public IRandomGenerator Random;
		public ITraceService Trace;

		internal readonly LateMutationFieldUpdate loadSizeUpdate = new LateMutationFieldUpdate();
		internal readonly LateMutationFieldUpdate loadSeedUpdate = new LateMutationFieldUpdate();
	}
}
