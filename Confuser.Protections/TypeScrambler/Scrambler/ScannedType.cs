using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal sealed class ScannedType : ScannedItem {
		internal TypeDef TargetType { get; private set; }

		public ScannedType(TypeDef target) : base(target) {
			Debug.Assert(target != null, $"{nameof(target)} != null");

			TargetType = target;
		}

		internal override void Scan() {
			foreach (var field in TargetType.Fields)
				RegisterGeneric(field.FieldType);
		}

		protected override void PrepareGenerics(IEnumerable<GenericParam> scrambleParams) {
			Debug.Assert(scrambleParams != null, $"{nameof(scrambleParams)} != null");
			if (!IsScambled) return;

			TargetType.GenericParameters.Clear();
			foreach (var generic in scrambleParams)
				TargetType.GenericParameters.Add(generic);

			foreach (var field in TargetType.Fields)
				field.FieldType = ConvertToGenericIfAvalible(field.FieldType);
		}

		internal GenericInstSig CreateGenericTypeSig(ScannedType from) => new GenericInstSig(GetTarget(), TrueTypes.ToList());

		internal override IMemberDef GetMemberDef() => TargetType;

		internal override ClassOrValueTypeSig GetTarget() => TargetType.ToTypeSig().ToClassOrValueTypeSig();
	}
}
