using System;
using System.Runtime.InteropServices;

namespace SecsLite.Model
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate void HsmsOnClose(IntPtr hSecs);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate bool HsmsOnConnect(IntPtr hServer, IntPtr hSecs, ushort usDevId);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate uint HsmsOnRecv(IntPtr hSecs, uint dwId, byte ucS, byte ucF, IntPtr data);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int HsmsOnRecvRawData(IntPtr hSecs, uint dwId, byte ucS, byte ucF, IntPtr data, uint dataLen);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate void HsmsOnReplyTimeout(IntPtr hSecs, uint dwId);

	internal struct SecsSets
	{

		//HSMS 参数
		public uint t3;

		public uint t5;

		public uint t6;

		public uint t7;

		public uint t8;

		public uint LinkTestInterval;

		public uint sLocalIP;

		public ushort usLocalPort;

		public uint sRemoteIP;

		public ushort usRemotePort;

		public ushort usDeviceID;

		public byte mode;

		public byte hsmsMode;


		//SECS I 参数
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
		public string szComPort;

        public uint baudRate;

		public uint ms;

		public uint t1;    
		
		public uint t2;   
		
		public uint t3_secsi;     
		
		public uint t4;


		//回调函数
		public HsmsOnConnect delegateOnConnect;

		public HsmsOnRecv delegateOnRecv;

		public HsmsOnRecvRawData delegateOnRecvRawData;

		public HsmsOnClose delegateOnClose;

		public HsmsOnReplyTimeout delegateOnReplyTimeout;
	}
}
