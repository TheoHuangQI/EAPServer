using Serilog;

namespace SecsLite
{
	internal static class SerilogConfigHelper
	{
		private static readonly ILogger SecsLogger = CreateLogger();
		private static readonly ILogger FdcLogger = CreateLogger();

		public static ILogger GetSecsLogger() => SecsLogger;

		public static ILogger GetFdcLogger() => FdcLogger;

		private static ILogger CreateLogger()
		{
			return new LoggerConfiguration()
				.MinimumLevel.Information()
				.CreateLogger();
		}
	}
}
