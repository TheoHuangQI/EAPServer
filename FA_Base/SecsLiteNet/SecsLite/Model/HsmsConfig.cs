using SecsLite.Gem;

namespace SecsLite.Model
{
	public class HsmsConfig
	{
		public uint T3 = 30000u;

		public uint T5 = 5000u;

		public uint T6 = 30000u;

		public uint T7 = 5000u;

		public uint T8 = 6000u;

		public uint LinkTestInterval = 60000u;

		public string LocalIP;

		public ushort LocalPort;

		public string RemoteIP;

		public ushort RemotePort;

		public HsmsMode Mode;

		public ushort DeviceID;
		public FSMCommunicationState.State DefaultCommunicationState= FSMCommunicationState.State.Enabled;
		public ControlStateEnum DefaultControlState=ControlStateEnum.Offline;
		public FSMControlState.State DefaultOnlineControlState= FSMControlState.State.OnlineRemote;
		public FSMControlState.State DefaultOfflineControlState= FSMControlState.State.AttemptOnline;
	}
}
