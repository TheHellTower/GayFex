using System;
using System.Diagnostics;

namespace Confuser.Helpers {
	public partial class InjectHelper {
		private sealed class ChildContextRelease : IDisposable {
			private readonly Action _releaseAction;
			private bool _disposed = false;

			internal ChildContextRelease(Action releaseAction) {
				Debug.Assert(releaseAction is not null, $"{nameof(releaseAction)} is not null");

				_releaseAction = releaseAction;
			}

			void Dispose(bool disposing) {
				if (!_disposed) {
					if (disposing) {
						_releaseAction.Invoke();
					}

					_disposed = true;
				}
			}

			void IDisposable.Dispose() {
				Dispose(true);
				GC.SuppressFinalize(this);
			}
		}
	}
}
