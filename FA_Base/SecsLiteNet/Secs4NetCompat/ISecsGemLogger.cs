namespace Secs4Net
{
	public interface ISecsGemLogger
	{
		void MessageIn(SecsMessage msg, int id)
		{
			System.Diagnostics.Trace.WriteLine($"<-- [0x{id:X8}] {msg}");
		}

		void MessageOut(SecsMessage msg, int id)
		{
			System.Diagnostics.Trace.WriteLine($"--> [0x{id:X8}] {msg}");
		}

		void Debug(string msg)
		{
			System.Diagnostics.Trace.WriteLine(msg);
		}

		void Info(string msg)
		{
			System.Diagnostics.Trace.TraceInformation(msg);
		}

		void Warning(string msg)
		{
			System.Diagnostics.Trace.TraceWarning(msg);
		}

		void Error(string msg)
		{
			Error(msg, null, null);
		}

		void Error(string msg, Exception ex)
		{
			Error(msg, null, ex);
		}

		void Error(string msg, SecsMessage message, Exception ex)
		{
			System.Diagnostics.Trace.TraceError($"{msg} {message}\n {ex}");
		}
	}
}
