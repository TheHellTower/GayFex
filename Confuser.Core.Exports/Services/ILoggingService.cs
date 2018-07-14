namespace Confuser.Core.Services {
	public interface ILoggingService {
		ILogger GetLogger();
		ILogger GetLogger(string tag);
	}
}
