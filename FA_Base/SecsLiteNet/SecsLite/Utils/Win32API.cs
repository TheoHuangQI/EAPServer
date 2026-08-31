using System.Runtime.InteropServices;

namespace SecsLite.Utils
{
	public static class Win32API
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetSystemTime(ref SYSTEMTIME st);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetLocalTime(ref SYSTEMTIME st);
	}
}
