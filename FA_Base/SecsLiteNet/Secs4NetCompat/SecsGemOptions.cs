namespace Secs4Net
{
	public class SecsGemOptions
	{
		public ushort DeviceId { get; set; }

		public bool IsActive { get; set; }

		public string IpAddress { get; set; } = "127.0.0.1";

		public int Port { get; set; } = 5000;

		public string LocalIpAddress { get; set; } = "0.0.0.0";

		public int LocalPort { get; set; }

		public int T3 { get; set; } = 45000;

		public int T5 { get; set; } = 10000;

		public int T6 { get; set; } = 5000;

		public int T7 { get; set; } = 10000;

		public int T8 { get; set; } = 5000;

		public int LinkTestInterval { get; set; } = 60000;

		public bool AutoReconnect { get; set; } = true;

		public int SocketReceiveBufferSize { get; set; } = 8192;

		public int EncodeBufferInitialSize { get; set; } = 4096;
	}
}
