namespace Secs4Net
{
	public enum DisconnectReason
	{
		Unknown,
		UserRequested,
		NetworkError,
		RemoteClosed,
		ConnectFailed,
		HSMSSelectFailed,
		ApplicationShutdown,
		InvalidConfiguration
	}
}
