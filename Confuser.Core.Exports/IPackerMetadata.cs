using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Core {
	public interface IPackerMetadata {
		string Id { get; }

		[DefaultValue(null)]
		string MarkerId { get; }
	}
}
