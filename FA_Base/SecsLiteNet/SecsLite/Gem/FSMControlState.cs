using FSMLite;
using SecsLite.Gem;
using SecsLite.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsLite.Gem
{
	public class FSMControlState : FSMBase
	{
		public enum State : ushort
		{
			Undefined,
			EqOffline,
			AttemptOnline,
			HostOffline,
			OnlineLocal,
			OnlineRemote
		}

		public enum Event : ushort
		{
			ControlInitialization,
			OperatorSwitchToOnline,
			ReveiveMessageS1F0,
			S1F1ReplyTimeOut,
			OperatorSwitchToOffline,
			OnlineConfirmRemote,
			OnlineConfirmLocal,
			OperatorSwitchToLocal,
			OperatorSwitchToRemote,
			HostOfflineRequest,
			HostOnlineRequest,
			ControlUndfined
		}

		public State ControlState => (State)base.CurrentState;

		internal State DefaultState { get; set; }
		internal State DefaultOnlineState { get; set; }
		public FSMControlState(object x, State defaultState, State defaultOnlineState)
			: base(x, FSMRunMode.Basic)
		{
			base.Name = "GEM.Control";
			DefaultState = defaultState;
			DefaultOnlineState = defaultOnlineState;
			defineTransitionTable();
		}
		internal void UndefineTransitionTable()
        {
			UnDefineTransition();
		}
		internal void defineTransitionTable()
        {
			DefineTransition(State.Undefined, Event.ControlInitialization, DefaultState, OnInitialization);
			DefineTransition(State.EqOffline, Event.OperatorSwitchToOnline, State.AttemptOnline, OnAttemptOnline);
			DefineTransition(State.AttemptOnline, Event.ReveiveMessageS1F0, State.HostOffline, OnAttemptToHostOffline);
			DefineTransition(State.AttemptOnline, Event.S1F1ReplyTimeOut, State.EqOffline, OnEqOffline);
			DefineTransition(State.AttemptOnline, Event.OnlineConfirmLocal, State.OnlineLocal, OnToOnlineDefault);
			DefineTransition(State.AttemptOnline, Event.OnlineConfirmRemote, State.OnlineRemote, OnToOnlineDefault);
			DefineTransition(State.AttemptOnline, Event.OperatorSwitchToOffline, State.EqOffline, OnEqOffline);
			DefineTransition(State.AttemptOnline, Event.OperatorSwitchToOnline, State.AttemptOnline, OnAttemptOnline);
			DefineTransition(State.HostOffline, Event.OperatorSwitchToOnline, State.AttemptOnline, OnAttemptOnline);
			DefineTransition(State.HostOffline, Event.HostOnlineRequest, State.AttemptOnline, OnAttemptOnline);
			DefineTransition(State.HostOffline, Event.OperatorSwitchToOffline, State.EqOffline, OnEqOffline);

			DefineTransition(State.OnlineLocal, Event.OperatorSwitchToRemote, State.OnlineRemote, OnToRemote);
			DefineTransition(State.OnlineLocal, Event.HostOfflineRequest, State.HostOffline, OnToOffline);
			DefineTransition(State.OnlineLocal, Event.OperatorSwitchToOffline, State.EqOffline, OnToOffline);
			DefineTransition(State.OnlineRemote, Event.OperatorSwitchToLocal, State.OnlineLocal, OnToLocal);
			DefineTransition(State.OnlineRemote, Event.HostOfflineRequest, State.HostOffline, OnToOffline);
			DefineTransition(State.OnlineRemote, Event.OperatorSwitchToOffline, State.EqOffline, OnToOffline);
			
			DefineTransition(State.EqOffline, Event.OperatorSwitchToLocal, State.OnlineLocal, OnToLocal);
			DefineTransition(State.EqOffline, Event.OperatorSwitchToRemote, State.OnlineRemote, OnToRemote);

			DefineTransition(State.EqOffline, Event.ControlUndfined, State.Undefined, OnControlUndfined);
			DefineTransition(State.AttemptOnline, Event.ControlUndfined, State.Undefined, OnControlUndfined);
			DefineTransition(State.HostOffline, Event.ControlUndfined, State.Undefined, OnControlUndfined);
			DefineTransition(State.OnlineLocal, Event.ControlUndfined, State.Undefined, OnControlUndfined);
			DefineTransition(State.OnlineRemote, Event.ControlUndfined, State.Undefined, OnControlUndfined);

			//DefineTransition(State.EqOffline, Event.HostOnlineRequest, DefaultOnlineState, OnToOnlineDefault);
			//DefineTransition(State.AttemptOnline, Event.HostOnlineRequest, DefaultOnlineState, OnToOnlineDefault);
		}

		private static void OnInitialization(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			gemEquipment.PostNamedEvent(EventName.ControlToOffline);
			if (gemEquipment.ControlState == State.AttemptOnline)
			{
				if (gemEquipment.msgSend_S1F1() == 0)
				{
					gemEquipment.CtrlStateFSM.TriggerEvent(Event.S1F1ReplyTimeOut);
					if (gemEquipment.m_msgId_RevS1F13 != 0u)
					{
						gemEquipment.ReplyS1F14();
					}
				}
			}
            else
            {
				if (gemEquipment.m_msgId_RevS1F13 != 0u)
				{
					gemEquipment.ReplyS1F14();
				}
			}
		}

		private static void OnAttemptOnline(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			if (gemEquipment.msgSend_S1F1() == 0)
			{
				gemEquipment.CtrlStateFSM.TriggerEvent(Event.S1F1ReplyTimeOut);
			}
		}

		private static void OnAttemptToHostOffline(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			if (gemEquipment.m_msgId_RevS1F13 != 0u)
			{
				gemEquipment.ReplyS1F14();
			}
		}

		private static void OnHostOffline(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			gemEquipment.PostNamedEvent(EventName.ControlToOffline);
		}

		private static void OnEqOffline(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
		}

		private static void OnToOnlineDefault(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			if (gemEquipment.CtrlStateFSM.DefaultOnlineState == State.OnlineLocal)
			{
				gemEquipment.PostNamedEvent(EventName.ControlToOnlineLocal);
			}
			if (gemEquipment.CtrlStateFSM.DefaultOnlineState == State.OnlineRemote)
			{
				gemEquipment.PostNamedEvent(EventName.ControlToOnlineRemote);
			}
			if (gemEquipment.m_msgId_RevS1F13 != 0u)
			{
				gemEquipment.ReplyS1F14();
			}
			gemEquipment.CtrlStateFSM.DefaultOnlineState = gemEquipment.HsmsCfg.DefaultOnlineControlState;
		}

		private static void OnToLocal(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			gemEquipment.PostNamedEvent(EventName.ControlToOnlineLocal);
		}
		private static void OnToRemote(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			gemEquipment.PostNamedEvent(EventName.ControlToOnlineRemote);
		}

		private static void OnToOffline(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
			gemEquipment.PostNamedEvent(EventName.ControlToOffline);
		}

		private static void OnControlUndfined(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnControlStateChange((int)gemEquipment.ControlState);
		}
	}
}
