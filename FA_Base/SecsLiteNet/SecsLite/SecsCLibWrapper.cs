using SecsLite.Model;
using System;
using System.Runtime.InteropServices;

namespace SecsLite
{
	internal static class SecsCLibWrapper
	{
		private const string DLL_NAME = "secslite.dll";

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern IntPtr CreateSecs(ref SecsSets sSet);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern void DestroySecs(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SecsStartupServer(IntPtr hServer);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern void SecsCloseServer(IntPtr hServer);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SecsConnect(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern void SecsDisconnect(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern uint SecsSendMessageBuffer(IntPtr hSecs, byte stream, byte func, IntPtr dataBuffer, int dataLen, uint flag, uint msgId);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IfSecsconnect(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IfSecsSelected(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern int SecsGetLastError(IntPtr hSecs);

		[DllImport("secslite.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern int SecsGetLastErrorMessage(IntPtr hSecs, byte[] buffer, int bufferSize);
	}
}
