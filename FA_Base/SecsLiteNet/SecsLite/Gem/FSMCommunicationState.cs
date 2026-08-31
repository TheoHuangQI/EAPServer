using FALogger;
using FSMLite;
using SecsLite.Gem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecsLite.Gem
{
	public class FSMCommunicationState : FSMBase
	{
		public enum State : ushort
		{
			Disabled,
			Enabled,
			NotCommunicating,
			Communicating
		}

		public enum Event : ushort
		{
			CommuInitialization,
			EnableCommunication,
			DisableCommunication,
			EntryToEnabled,
			CommunicationSuccess,
			CommunicationFailure,
		}

		
		public State DefaultState { get; set; }

		public FSMCommunicationState(object x, State defaultCommunicationState)
			: base(x, FSMRunMode.Basic)
		{
			base.Name = "GEM.Communicating";
			DefaultState = defaultCommunicationState;
			defineTransitionTable();
		}

		internal void UndefineTransitionTable()
		{
			UnDefineTransition();
		}

		internal void defineTransitionTable()
		{
			DefineTransition(State.Disabled, Event.CommuInitialization, DefaultState, OnInitialization);
			DefineTransition(State.Disabled, Event.EnableCommunication, State.Enabled, OnEnableCommunication);
			DefineTransition(State.Enabled, Event.EntryToEnabled, State.NotCommunicating, OnNotCommunicating);
			DefineTransition(State.NotCommunicating, Event.CommunicationSuccess, State.Communicating, OnInitiatedCommunicating);

			DefineTransition(State.Enabled, Event.DisableCommunication, State.Disabled, OnDisableCommunication);
			DefineTransition(State.NotCommunicating, Event.DisableCommunication, State.Disabled, OnDisableCommunication);
			DefineTransition(State.Communicating, Event.DisableCommunication, State.Disabled, OnDisableCommunication);

			DefineTransition(State.Enabled, Event.CommunicationFailure, State.NotCommunicating, OnCommunicationFail);
			DefineTransition(State.NotCommunicating, Event.CommunicationFailure, State.NotCommunicating, OnCommunicationFail);
			DefineTransition(State.Communicating, Event.CommunicationFailure, State.NotCommunicating, OnCommunicationFail);
		}


		private static void OnInitialization(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			if (gemEquipment.HsmsCfg.DefaultCommunicationState == State.Enabled)
            {
				gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
				gemEquipment.CommStateFSM.TriggerEvent(Event.EntryToEnabled);
				gemEquipment.Start(gemEquipment.HsmsCfg);
			}
		}

		private static void OnEnableCommunication(object arg)
		{

			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
			gemEquipment.CommStateFSM.TriggerEvent(Event.EntryToEnabled);
			gemEquipment.Start(gemEquipment.HsmsCfg);
		}

		private static void OnCommunicationFail(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
			gemEquipment.CommStateEquipFSM.TriggerEvent(FSMNotCommunicating_EquipState.Event.CommunicationFailure);
			gemEquipment.CommStateHostFSM.TriggerEvent(FSMNotCommunicating_HostState.Event.CommunicationFailure);
			Thread.Sleep(1000);
			//进入双向通讯
			gemEquipment.CommStateHostFSM.TriggerEvent(FSMNotCommunicating_HostState.Event.HostInitiatedConnect);
			gemEquipment.CommStateEquipFSM.TriggerEvent(FSMNotCommunicating_EquipState.Event.EquipmentInitiatedConnect);
		}

		private static void OnNotCommunicating(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
			
			//进入双向通讯
			gemEquipment.CommStateHostFSM.TriggerEvent(FSMNotCommunicating_HostState.Event.HostInitiatedConnect);
			gemEquipment.CommStateEquipFSM.TriggerEvent (FSMNotCommunicating_EquipState.Event.EquipmentInitiatedConnect);
		}

		private static void OnInitiatedCommunicating(object arg)
        {
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
			gemEquipment.DefaultControlState();
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
		}

		private static void OnDisableCommunication(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.OnCommunicationStateChange((int)gemEquipment.CommunicationState);
			gemEquipment.CommStateEquipFSM.TriggerEvent(FSMNotCommunicating_EquipState.Event.DisableCommunication);
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
			gemEquipment.CommStateHostFSM.TriggerEvent(FSMNotCommunicating_HostState.Event.DisableCommunication);
			gemEquipment.Stop();
			gemEquipment.OnCommDisabled();


		}

	}

	public class FSMNotCommunicating_HostState : FSMBase
    {
		public enum State : ushort
		{
			WaitCRFromHost,
			Communicating
		}

		public enum Event : ushort
		{
			HostInitiatedConnect,
			ReceiveCommRequest,
			DisableCommunication,
			CommunicationFailure,
			CommunicationSuccess
		}

		public FSMNotCommunicating_HostState(object x)
			: base(x, FSMRunMode.Basic)
		{
			base.Name = "GEM.CommunicatingHost";
			DefineTransition(State.WaitCRFromHost, Event.HostInitiatedConnect, State.WaitCRFromHost, null);
			DefineTransition(State.WaitCRFromHost, Event.ReceiveCommRequest, State.Communicating, OnHostRequestCommunicating);

			DefineTransition(State.WaitCRFromHost, Event.DisableCommunication, State.WaitCRFromHost, null);
			DefineTransition(State.Communicating, Event.DisableCommunication, State.WaitCRFromHost, null);
			DefineTransition(State.WaitCRFromHost, Event.CommunicationFailure, State.WaitCRFromHost, null);
			DefineTransition(State.Communicating, Event.CommunicationFailure, State.WaitCRFromHost, null);
			DefineTransition(State.WaitCRFromHost, Event.CommunicationSuccess, State.Communicating, OnHostRequestCommunicating);
		}

		private static void OnHostRequestCommunicating(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.CommStateFSM.TriggerEvent(FSMCommunicationState.Event.CommunicationSuccess);
			gemEquipment.CommStateEquipFSM.TriggerEvent(FSMNotCommunicating_EquipState.Event.CommunicationSuccess);
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
		}
	}

	public class FSMNotCommunicating_EquipState : FSMBase
	{
		private Timer m_timerCommDelay;

		public enum State : ushort
		{
			WaitCRA,
			WaitDelay,
			Communicating
		}

		public enum Event : ushort
		{
			EquipmentInitiatedConnect,
			CommunicationDelayTimerExpired,
			ReveiveMessageOtherThanS1F13,
			ReveiveMessageS1F14,
			ConnectionTransactionFailure,
			DisableCommunication,
			CommunicationFailure, 
			CommunicationSuccess
		}

		public FSMNotCommunicating_EquipState(object x)
			: base(x, FSMRunMode.Thread)
		{
			base.Name = "GEM.CommunicatingEqp";
			DefineTransition(State.WaitCRA, Event.EquipmentInitiatedConnect, State.WaitCRA, OnWaitCRA);
			DefineTransition(State.WaitCRA, Event.ConnectionTransactionFailure, State.WaitDelay, OnConnectionFailure);
			DefineTransition(State.WaitCRA, Event.CommunicationDelayTimerExpired, State.WaitCRA, OnWaitCRA);
			DefineTransition(State.WaitDelay, Event.CommunicationDelayTimerExpired, State.WaitCRA, OnWaitCRA);
			DefineTransition(State.WaitCRA, Event.ReveiveMessageS1F14, State.Communicating, OnEquipmentInitiatedCommunicating);

			DefineTransition(State.WaitCRA, Event.DisableCommunication, State.WaitCRA, OnDisable);
			DefineTransition(State.WaitDelay, Event.DisableCommunication, State.WaitCRA, OnDisable);
			DefineTransition(State.Communicating, Event.DisableCommunication, State.WaitCRA, OnDisable);

			DefineTransition(State.WaitCRA, Event.CommunicationFailure, State.WaitCRA, OnDisable);
			DefineTransition(State.WaitDelay, Event.CommunicationFailure, State.WaitCRA, OnDisable);
			DefineTransition(State.Communicating, Event.CommunicationFailure, State.WaitCRA, OnDisable);

			DefineTransition(State.WaitCRA, Event.CommunicationSuccess, State.Communicating, OnCommunicating);
			DefineTransition(State.WaitDelay, Event.CommunicationSuccess, State.Communicating, OnCommunicating);

		}

		private static void OnWaitCRA(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.CommStateEquipFSM.StartCommDelayTimer();
            if (gemEquipment.SendS1F13() == 0)
            {
                gemEquipment.CommStateEquipFSM.TriggerEvent(Event.ConnectionTransactionFailure);
            }
        }

		private static void OnConnectionFailure(object arg)
		{
			GemEquipment gem = arg as GemEquipment;
			gem.CommStateEquipFSM.StopCommDelayTimer();
			gem.CommStateEquipFSM.StartCommDelayTimer(); // 启动延时计时器
		}

		private static void OnEquipmentInitiatedCommunicating(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.CommStateFSM.TriggerEvent(FSMCommunicationState.Event.CommunicationSuccess);
			gemEquipment.CommStateHostFSM.TriggerEvent(FSMNotCommunicating_HostState.Event.CommunicationSuccess);
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
		}
		private static void OnCommunicating(object arg)
		{
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
		}
		
		private static void OnDisable(object arg)
        {
			GemEquipment gemEquipment = arg as GemEquipment;
			gemEquipment.CommStateEquipFSM.StopCommDelayTimer();
		}
		private static void commDelayTimerCallback(object arg)
		{
			FSMNotCommunicating_EquipState fSMNotCommunicating_EquipState = arg as FSMNotCommunicating_EquipState;
			fSMNotCommunicating_EquipState.TriggerEvent(Event.CommunicationDelayTimerExpired);
		}
		

		public void StartCommDelayTimer()
		{
			if (m_timerCommDelay == null)
			{
				m_timerCommDelay = new Timer(commDelayTimerCallback, this, -1, -1);
			}
			GemEquipment gemEquipment = base.ActionHost as GemEquipment;
			m_timerCommDelay.Change(gemEquipment.EstablishCommunicationsTimeout * 1000, 0);
		}

		public void StopCommDelayTimer()
		{
			if (m_timerCommDelay != null)
			{
				m_timerCommDelay.Change(-1, -1);
			}
		}
	}
}
