using System;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	public struct StrongNameData {
		public StrongNameKey SnKey;
		public StrongNamePublicKey SnPubKey;
		public StrongNameKey SnSigKey;
		public StrongNamePublicKey SnSigPubKey;
		public bool SnDelaySign;
	}
}
