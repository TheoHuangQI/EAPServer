namespace Secs4Net
{
	public sealed class SecsException : Exception
	{
		public SecsException(string message)
			: base(message)
		{
		}

		public SecsException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
