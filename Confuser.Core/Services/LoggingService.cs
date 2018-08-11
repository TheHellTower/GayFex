using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Core.Services {
	internal sealed class LoggingService : ILoggingService {
		private readonly ILogger internalLogger;

		internal LoggingService(ILogger internalLogger) {
			this.internalLogger = internalLogger ?? throw new ArgumentNullException(nameof(internalLogger));
		}

		public ILogger GetLogger() => internalLogger;

		public ILogger GetLogger(string tag) => internalLogger;
	}
}
