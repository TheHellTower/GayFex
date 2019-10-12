using System;
using System.Collections.Generic;
using Confuser.Core;

namespace ConfuserEx {
	[Serializable]
	public sealed class ConfuserUiComponent : IEquatable<ConfuserUiComponent> {
		public string Id { get; }
		public string Name { get; }
		public string Description { get; }

		public string PlugInPath { get; }

		internal ConfuserUiComponent(Lazy<IProtection, IProtectionMetadata> protection) {
			Id = protection.Metadata.MarkerId ?? protection.Metadata.Id;
			Name = protection.Value.Name;
			Description = protection.Value.Description;
			PlugInPath = protection.Value.GetType().Assembly.Location;
		}

		internal ConfuserUiComponent(Lazy<IPacker, IPackerMetadata> packer) {
			Id = packer.Metadata.MarkerId ?? packer.Metadata.Id;
			Name = packer.Value.Name;
			Description = packer.Value.Description;
			PlugInPath = packer.Value.GetType().Assembly.Location;
		}

		public bool Equals(ConfuserUiComponent other) {
			if (other == null) return false;

			if (!string.Equals(Id, other.Id, StringComparison.Ordinal)) return false;
			if (!string.Equals(PlugInPath, other.PlugInPath, StringComparison.OrdinalIgnoreCase)) return false;

			return true;
		}

		public override bool Equals(object obj) => Equals(obj as ConfuserUiComponent);

		public override int GetHashCode() => (Id, PlugInPath.GetHashCode(StringComparison.OrdinalIgnoreCase)).GetHashCode();
	}
}
