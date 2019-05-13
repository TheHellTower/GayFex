using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	internal sealed class TraceService : ITraceService {
		private readonly Dictionary<MethodDef, MethodTrace> _cache = new Dictionary<MethodDef, MethodTrace>();

		/// <inheritdoc />
		public IMethodTrace Trace(MethodDef method) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			return _cache.GetValueOrDefaultLazy(method, m => _cache[m] = new MethodTrace(m)).Trace();
		}
	}
}
